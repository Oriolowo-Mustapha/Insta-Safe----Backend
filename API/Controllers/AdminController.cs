using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Admin.Commands.ForceRelease;
using InstaSafe.Application.Features.Admin.Commands.SetDispatcherActive;
using InstaSafe.Application.Features.Admin.DTOs;
using InstaSafe.Application.Features.Admin.Queries.GetAdminStats;
using InstaSafe.Application.Features.Admin.Queries.GetOutboxStatus;
using InstaSafe.Application.Features.Admin.Queries.ListAllDispatchers;
using InstaSafe.Application.Features.Admin.Queries.ListAllOrders;
using InstaSafe.Application.Features.Admin.Queries.ListAllVendors;
using InstaSafe.Application.Features.Admin.Queries.ListAuditLog;
using InstaSafe.Application.Features.Admin.Queries.ListDisputes;
using InstaSafe.Application.Features.Admin.Queries.SearchChats;
using InstaSafe.Application.Features.Admin.Queries.SearchWebhooks;
using InstaSafe.Application.Features.Dispatch.DTOs;
using InstaSafe.Application.Features.Orders.Commands.RefundOrder;
using InstaSafe.Application.Features.Orders.Commands.ResolveDispute;
using InstaSafe.Application.Features.Orders.DTOs;
using InstaSafe.Application.Features.Orders.Queries.GetOrderById;
using InstaSafe.Application.Features.Vendors.Commands.DeactivateVendor;
using InstaSafe.Application.Features.Vendors.Commands.ReactivateVendor;
using InstaSafe.Application.Features.Vendors.Commands.UpdateVendorPhone;
using InstaSafe.Application.Features.Vendors.DTOs;
using InstaSafe.Application.Features.Vendors.Queries.GetVendorById;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InstaSafe.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;

    public AdminController(IMediator mediator, IAppDbContext db)
    {
        _mediator = mediator; _db = db;
    }

    private async Task AuditAsync(string action, string targetType, string targetId, string? note, CancellationToken ct)
    {
        var actor = User.FindFirst(ClaimTypes.MobilePhone)?.Value ?? "admin";
        _db.AdminAuditLogs.Add(new AdminAuditLog
        {
            Actor = actor, Action = action, TargetType = targetType, TargetId = targetId, Note = note
        });
        await _db.SaveChangesAsync(ct);
    }

    [HttpGet("stats")]
    public async Task<ActionResult<ApiResponse<AdminStatsDto>>> Stats(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAdminStatsQuery(), ct);
        return Ok(ApiResponse<AdminStatsDto>.FromResult(result));
    }

    [HttpGet("vendors")]
    public async Task<ActionResult<ApiResponse<List<VendorDto>>>> Vendors(
        [FromQuery] string? q = null, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListAllVendorsQuery(page, pageSize, q), ct);
        return Ok(ApiResponse<List<VendorDto>>.FromResult(result));
    }

    [HttpGet("vendors/{id:guid}")]
    public async Task<ActionResult<ApiResponse<VendorDto>>> VendorById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetVendorByIdQuery(id), ct);
        if (!result.IsSuccess) return NotFound(ApiResponse<VendorDto>.FromResult(result));
        return Ok(ApiResponse<VendorDto>.FromResult(result));
    }

    [HttpPost("vendors/{id:guid}/deactivate")]
    public async Task<ActionResult<ApiResponse<VendorDto>>> DeactivateVendor(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeactivateVendorCommand(id), ct);
        if (!result.IsSuccess) return NotFound(ApiResponse<VendorDto>.FromResult(result));
        await AuditAsync("vendor.deactivate", "vendor", id.ToString(), null, ct);
        return Ok(ApiResponse<VendorDto>.FromResult(result, "Vendor deactivated."));
    }

    [HttpPost("vendors/{id:guid}/reactivate")]
    public async Task<ActionResult<ApiResponse<VendorDto>>> ReactivateVendor(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ReactivateVendorCommand(id), ct);
        if (!result.IsSuccess) return NotFound(ApiResponse<VendorDto>.FromResult(result));
        await AuditAsync("vendor.reactivate", "vendor", id.ToString(), null, ct);
        return Ok(ApiResponse<VendorDto>.FromResult(result, "Vendor reactivated."));
    }

    [HttpPut("vendors/{id:guid}/phone")]
    public async Task<ActionResult<ApiResponse<VendorDto>>> CorrectVendorPhone(
        Guid id, [FromBody] PhoneRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateVendorPhoneCommand(id, body.Phone), ct);
        if (!result.IsSuccess) return BadRequest(ApiResponse<VendorDto>.FromResult(result));
        await AuditAsync("vendor.correct-phone", "vendor", id.ToString(), body.Phone, ct);
        return Ok(ApiResponse<VendorDto>.FromResult(result, "Vendor phone corrected."));
    }

    [HttpGet("dispatchers")]
    public async Task<ActionResult<ApiResponse<List<DispatcherDto>>>> Dispatchers(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListAllDispatchersQuery(page, pageSize), ct);
        return Ok(ApiResponse<List<DispatcherDto>>.FromResult(result));
    }

    [HttpPost("dispatchers/{id:guid}/deactivate")]
    public async Task<ActionResult<ApiResponse<DispatcherDto>>> DeactivateDispatcher(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new SetDispatcherActiveCommand(id, false), ct);
        if (!result.IsSuccess) return NotFound(ApiResponse<DispatcherDto>.FromResult(result));
        await AuditAsync("dispatcher.deactivate", "dispatcher", id.ToString(), null, ct);
        return Ok(ApiResponse<DispatcherDto>.FromResult(result, "Dispatcher deactivated."));
    }

    [HttpPost("dispatchers/{id:guid}/reactivate")]
    public async Task<ActionResult<ApiResponse<DispatcherDto>>> ReactivateDispatcher(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new SetDispatcherActiveCommand(id, true), ct);
        if (!result.IsSuccess) return NotFound(ApiResponse<DispatcherDto>.FromResult(result));
        await AuditAsync("dispatcher.reactivate", "dispatcher", id.ToString(), null, ct);
        return Ok(ApiResponse<DispatcherDto>.FromResult(result, "Dispatcher reactivated."));
    }

    [HttpGet("orders")]
    public async Task<ActionResult<ApiResponse<List<OrderDto>>>> Orders(
        [FromQuery] OrderStatus? status = null, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, [FromQuery] string? q = null, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListAllOrdersQuery(status, page, pageSize, q), ct);
        return Ok(ApiResponse<List<OrderDto>>.FromResult(result));
    }

    [HttpGet("orders/{id:guid}")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> OrderById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetOrderByIdQuery(id), ct);
        if (!result.IsSuccess) return NotFound(ApiResponse<OrderDto>.FromResult(result));
        return Ok(ApiResponse<OrderDto>.FromResult(result));
    }

    [HttpGet("disputes")]
    public async Task<ActionResult<ApiResponse<List<OrderDto>>>> Disputes(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListDisputesQuery(page, pageSize), ct);
        return Ok(ApiResponse<List<OrderDto>>.FromResult(result));
    }

    [HttpPost("orders/{id:guid}/resolve-dispute")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> ResolveDispute(
        Guid id, [FromBody] ResolveRequest body, CancellationToken ct)
    {
        if (!Enum.TryParse<DisputeResolution>(body.Resolution, true, out var resolution))
            return BadRequest(ApiResponse<OrderDto>.FailureResponse("Resolution must be 'release' or 'refund'."));
        var result = await _mediator.Send(new ResolveDisputeCommand(id, resolution), ct);
        if (!result.IsSuccess) return BadRequest(ApiResponse<OrderDto>.FromResult(result));
        await AuditAsync("order.resolve-dispute", "order", id.ToString(), resolution.ToString(), ct);
        return Ok(ApiResponse<OrderDto>.FromResult(result, $"Dispute resolved: {resolution}."));
    }

    [HttpPost("orders/{id:guid}/refund")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> Refund(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new RefundOrderCommand(id), ct);
        if (!result.IsSuccess) return BadRequest(ApiResponse<OrderDto>.FromResult(result));
        await AuditAsync("order.refund", "order", id.ToString(), null, ct);
        return Ok(ApiResponse<OrderDto>.FromResult(result, "Order refunded."));
    }

    [HttpPost("orders/{id:guid}/force-release")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> ForceRelease(
        Guid id, [FromBody] ForceReleaseRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new ForceReleaseOrderCommand(id, body?.Note), ct);
        if (!result.IsSuccess) return BadRequest(ApiResponse<OrderDto>.FromResult(result));
        await AuditAsync("order.force-release", "order", id.ToString(), body?.Note, ct);
        return Ok(ApiResponse<OrderDto>.FromResult(result, "Funds force-released to vendor."));
    }

    [HttpGet("chats")]
    public async Task<ActionResult<ApiResponse<List<ChatMessageDto>>>> Chats(
        [FromQuery] string? phone = null, [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new SearchChatsQuery(phone, from, to, page, pageSize), ct);
        return Ok(ApiResponse<List<ChatMessageDto>>.FromResult(result));
    }

    [HttpGet("webhooks")]
    public async Task<ActionResult<ApiResponse<List<WebhookEventDto>>>> Webhooks(
        [FromQuery] string? provider = null, [FromQuery] string? @event = null,
        [FromQuery] bool? validOnly = null, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new SearchWebhooksQuery(provider, @event, validOnly, page, pageSize), ct);
        return Ok(ApiResponse<List<WebhookEventDto>>.FromResult(result));
    }

    [HttpGet("outbox")]
    public async Task<ActionResult<ApiResponse<OutboxStatusDto>>> Outbox(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetOutboxStatusQuery(), ct);
        return Ok(ApiResponse<OutboxStatusDto>.FromResult(result));
    }

    [HttpGet("audit")]
    public async Task<ActionResult<ApiResponse<List<AdminAuditDto>>>> Audit(
        [FromQuery] string? action = null, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListAuditLogQuery(action, page, pageSize), ct);
        return Ok(ApiResponse<List<AdminAuditDto>>.FromResult(result));
    }

    public sealed record PhoneRequest(string Phone);
    public sealed record ResolveRequest(string Resolution);
    public sealed record ForceReleaseRequest(string? Note);
}
