using InstaSafe.Application.Common.Helpers;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.Commands.CreateOrder;
using InstaSafe.Application.Features.Orders.Commands.ParseOrderText;
using InstaSafe.Application.Features.Orders.Commands.ConfirmSatisfaction;
using InstaSafe.Application.Features.Orders.Commands.DisputeOrder;
using InstaSafe.Application.Features.Orders.Commands.RefundOrder;
using InstaSafe.Application.Features.Orders.Commands.RequestBankTransfer;
using InstaSafe.Application.Features.Orders.Commands.ResolveDispute;
using InstaSafe.Application.Features.Orders.Commands.VerifyOtp;
using InstaSafe.Application.Features.Orders.DTOs;
using InstaSafe.Application.Features.Orders.Queries.GetOrderById;
using InstaSafe.Application.Features.Orders.Queries.GetOrderByReference;
using InstaSafe.Application.Features.Orders.Queries.GetOrderTimeline;
using InstaSafe.Application.Features.Vendors.Queries.GetVendorOrders;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstaSafe.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;
    public OrdersController(IMediator mediator) => _mediator = mediator;

    private bool Owns(OrderDto order)
    {
        var vendorId = User.VendorId();
        var phone = User.VendorPhone();
        if (vendorId is null || phone is null) return false;
        return order.VendorPhone == phone;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<OrderDto>>> Create(
        [FromBody] CreateOrderCommand cmd, CancellationToken ct)
    {
        var claimPhone = User.VendorPhone();
        if (claimPhone is null ||
            PhoneNormalizer.Normalize(cmd.VendorPhone) != PhoneNormalizer.Normalize(claimPhone))
            return Forbid();
        var result = await _mediator.Send(cmd, ct);
        if (!result.IsSuccess) return BadRequest(ApiResponse<OrderDto>.FromResult(result));
        return Ok(ApiResponse<OrderDto>.FromResult(result, "Order created. Payment link sent to the customer via WhatsApp and email."));
    }

    [HttpPost("parse")]
    public async Task<ActionResult<ApiResponse<Application.Common.Interfaces.ParsedOrder>>> Parse(
        [FromBody] ParseOrderTextCommand cmd, CancellationToken ct)
    {
        var result = await _mediator.Send(cmd, ct);
        return Ok(ApiResponse<Application.Common.Interfaces.ParsedOrder>.FromResult(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetOrderByIdQuery(id), ct);
        if (!result.IsSuccess) return NotFound(ApiResponse<OrderDto>.FromResult(result));
        if (!Owns(result.Value!)) return Forbid();
        return Ok(ApiResponse<OrderDto>.FromResult(result));
    }

    [HttpGet("by-reference/{reference}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PublicOrderDto>>> GetByReference(string reference, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetOrderByReferenceQuery(reference), ct);
        if (!result.IsSuccess) return NotFound(ApiResponse<PublicOrderDto>.FromResult(result));
        return Ok(ApiResponse<PublicOrderDto>.FromResult(result));
    }

    /// <summary>Public customer tracker: ordered status timeline for an order reference. No auth.</summary>
    [HttpGet("by-reference/{reference}/timeline")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<OrderTimelineDto>>> GetTimeline(string reference, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetOrderTimelineQuery(reference), ct);
        if (!result.IsSuccess) return NotFound(ApiResponse<OrderTimelineDto>.FromResult(result));
        return Ok(ApiResponse<OrderTimelineDto>.FromResult(result));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<OrderDto>>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var vendorId = User.VendorId();
        if (vendorId is null) return Forbid();
        var result = await _mediator.Send(new GetVendorOrdersQuery(vendorId.Value, page, pageSize), ct);
        if (!result.IsSuccess) return NotFound(ApiResponse<List<OrderDto>>.FromResult(result));
        return Ok(ApiResponse<List<OrderDto>>.FromResult(result));
    }

    [HttpPost("{id:guid}/verify-otp")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PublicOrderDto>>> VerifyOtp(
        Guid id, [FromBody] VerifyOtpRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new VerifyOtpCommand(id, body.Otp), ct);
        if (!result.IsSuccess) return BadRequest(ApiResponse<PublicOrderDto>.FromResult(result));
        return Ok(ApiResponse<PublicOrderDto>.FromResult(result, "Funds released to vendor."));
    }

    /// <summary>
    /// Issue a dedicated bank-transfer account for this order.
    /// Buyer transfers the exact total; funds auto-confirm into escrow.
    /// </summary>
    [HttpPost("{id:guid}/request-bank-transfer")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> RequestBankTransfer(
        Guid id, [FromBody] BankTransferRequest body, CancellationToken ct)
    {
        var existing = await _mediator.Send(new GetOrderByIdQuery(id), ct);
        if (!existing.IsSuccess) return NotFound(ApiResponse<OrderDto>.FromResult(existing));
        if (!Owns(existing.Value!)) return Forbid();
        var result = await _mediator.Send(new RequestBankTransferCommand(id, body?.PreferredBank), ct);
        if (!result.IsSuccess) return BadRequest(ApiResponse<OrderDto>.FromResult(result));
        return Ok(ApiResponse<OrderDto>.FromResult(result, "Transfer account issued. Buyer notified."));
    }

    [HttpPost("{id:guid}/refund")]    public async Task<ActionResult<ApiResponse<OrderDto>>> Refund(Guid id, CancellationToken ct)
    {
        var existing = await _mediator.Send(new GetOrderByIdQuery(id), ct);
        if (!existing.IsSuccess) return NotFound(ApiResponse<OrderDto>.FromResult(existing));
        if (!Owns(existing.Value!)) return Forbid();
        var result = await _mediator.Send(new RefundOrderCommand(id), ct);
        if (!result.IsSuccess) return BadRequest(ApiResponse<OrderDto>.FromResult(result));
        return Ok(ApiResponse<OrderDto>.FromResult(result, "Order refunded."));
    }

    [HttpPost("{id:guid}/confirm-satisfaction")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PublicOrderDto>>> ConfirmSatisfaction(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ConfirmSatisfactionCommand(id), ct);
        if (!result.IsSuccess) return BadRequest(ApiResponse<PublicOrderDto>.FromResult(result));
        return Ok(ApiResponse<PublicOrderDto>.FromResult(result, "Order confirmed. Funds released to vendor."));
    }

    [HttpPost("{id:guid}/dispute")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PublicOrderDto>>> Dispute(
        Guid id, [FromBody] DisputeRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new DisputeOrderCommand(id, body.Reason), ct);
        if (!result.IsSuccess) return BadRequest(ApiResponse<PublicOrderDto>.FromResult(result));
        return Ok(ApiResponse<PublicOrderDto>.FromResult(result, "Dispute filed. Funds frozen until resolved."));
    }

    [HttpPost("{id:guid}/resolve-dispute")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> ResolveDispute(
        Guid id, [FromBody] ResolveDisputeRequest body, CancellationToken ct)
    {
        var existing = await _mediator.Send(new GetOrderByIdQuery(id), ct);
        if (!existing.IsSuccess) return NotFound(ApiResponse<OrderDto>.FromResult(existing));
        if (!Owns(existing.Value!)) return Forbid();
        if (!Enum.TryParse<DisputeResolution>(body.Resolution, true, out var resolution))
            return BadRequest(ApiResponse<OrderDto>.FailureResponse("Resolution must be 'release' or 'refund'."));
        var result = await _mediator.Send(new ResolveDisputeCommand(id, resolution), ct);
        if (!result.IsSuccess) return BadRequest(ApiResponse<OrderDto>.FromResult(result));
        return Ok(ApiResponse<OrderDto>.FromResult(result, $"Dispute resolved: {resolution}."));
    }

    public sealed record VerifyOtpRequest(string Otp);
    public sealed record DisputeRequest(string Reason);
    public sealed record ResolveDisputeRequest(string Resolution);
    public sealed record BankTransferRequest(string? PreferredBank);
}
