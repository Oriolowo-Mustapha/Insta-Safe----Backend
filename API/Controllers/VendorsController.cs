using InstaSafe.Application.Common.Helpers;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using InstaSafe.Application.Features.Vendors.Commands.DeactivateVendor;
using InstaSafe.Application.Features.Vendors.Commands.ReactivateVendor;
using InstaSafe.Application.Features.Vendors.Commands.RegisterVendor;
using InstaSafe.Application.Features.Vendors.Commands.UpdateVendorPayout;
using InstaSafe.Application.Features.Vendors.Commands.UpdateVendorPhone;
using InstaSafe.Application.Features.Vendors.Commands.UpdateVendorProfile;
using InstaSafe.Application.Features.Vendors.DTOs;
using InstaSafe.Application.Features.Vendors.Queries.GetVendorById;
using InstaSafe.Application.Features.Vendors.Queries.GetVendorByPhone;
using InstaSafe.Application.Features.Vendors.Queries.GetVendorOrders;
using InstaSafe.Application.Features.Vendors.Queries.ListVendors;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstaSafe.Api.Controllers;

[ApiController]
[Route("api/vendors")]
[Authorize]
public class VendorsController : ControllerBase
{
    private readonly IMediator _mediator;
    public VendorsController(IMediator mediator) => _mediator = mediator;

    private bool Owns(Guid id) => User.VendorId() == id;

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<VendorDto>>> Register(
        [FromBody] RegisterVendorCommand cmd, CancellationToken ct)
    {
        var result = await _mediator.Send(cmd, ct);
        if (!result.IsSuccess) return BadRequest(ApiResponse<VendorDto>.FromResult(result));
        return Ok(ApiResponse<VendorDto>.FromResult(result, "Vendor registered. Check your email for the verification code, then verify to continue."));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<VendorDto>>> GetById(Guid id, CancellationToken ct)
    {
        if (!Owns(id)) return Forbid();
        var result = await _mediator.Send(new GetVendorByIdQuery(id), ct);
        if (!result.IsSuccess) return NotFound(ApiResponse<VendorDto>.FromResult(result));
        return Ok(ApiResponse<VendorDto>.FromResult(result));
    }

    [HttpGet("by-phone")]
    public async Task<ActionResult<ApiResponse<VendorDto>>> GetByPhone(
        [FromQuery] string phone, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetVendorByPhoneQuery(phone), ct);
        if (!result.IsSuccess) return NotFound(ApiResponse<VendorDto>.FromResult(result));
        if (result.Value!.Id != User.VendorId()) return Forbid();
        return Ok(ApiResponse<VendorDto>.FromResult(result));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<VendorDto>>>> List(CancellationToken ct)
    {
        var self = User.VendorId();
        if (self is null) return Forbid();
        var result = await _mediator.Send(new GetVendorByIdQuery(self.Value), ct);
        if (!result.IsSuccess) return NotFound(ApiResponse<List<VendorDto>>.FailureResponse("Vendor not found."));
        return Ok(ApiResponse<List<VendorDto>>.SuccessResponse(new List<VendorDto> { result.Value! }));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<VendorDto>>> UpdateProfile(
        Guid id, [FromBody] UpdateProfileRequest body, CancellationToken ct)
    {
        if (!Owns(id)) return Forbid();
        var result = await _mediator.Send(new UpdateVendorProfileCommand(id, body.DisplayName), ct);
        if (!result.IsSuccess) return NotFound(ApiResponse<VendorDto>.FromResult(result));
        return Ok(ApiResponse<VendorDto>.FromResult(result, "Vendor updated."));
    }

    [HttpPut("{id:guid}/phone")]
    public async Task<ActionResult<ApiResponse<VendorDto>>> UpdatePhone(
        Guid id, [FromBody] UpdatePhoneRequest body, CancellationToken ct)
    {
        if (!Owns(id)) return Forbid();
        var result = await _mediator.Send(new UpdateVendorPhoneCommand(id, body.Phone), ct);
        if (!result.IsSuccess) return BadRequest(ApiResponse<VendorDto>.FromResult(result));
        return Ok(ApiResponse<VendorDto>.FromResult(result, "Phone updated. Log in again to refresh your session."));
    }

    [HttpPut("{id:guid}/payout")]
    public async Task<ActionResult<ApiResponse<VendorDto>>> UpdatePayout(
        Guid id, [FromBody] UpdatePayoutRequest body, CancellationToken ct)
    {
        if (!Owns(id)) return Forbid();
        var result = await _mediator.Send(
            new UpdateVendorPayoutCommand(id, body.AccountNumber, body.BankCode), ct);
        if (!result.IsSuccess) return NotFound(ApiResponse<VendorDto>.FromResult(result));
        return Ok(ApiResponse<VendorDto>.FromResult(result, "Payout details updated."));
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<ApiResponse<VendorDto>>> Deactivate(Guid id, CancellationToken ct)
    {
        if (!Owns(id)) return Forbid();
        var result = await _mediator.Send(new DeactivateVendorCommand(id), ct);
        if (!result.IsSuccess) return NotFound(ApiResponse<VendorDto>.FromResult(result));
        return Ok(ApiResponse<VendorDto>.FromResult(result, "Vendor deactivated."));
    }

    [HttpPost("{id:guid}/reactivate")]
    public async Task<ActionResult<ApiResponse<VendorDto>>> Reactivate(Guid id, CancellationToken ct)
    {
        if (!Owns(id)) return Forbid();
        var result = await _mediator.Send(new ReactivateVendorCommand(id), ct);
        if (!result.IsSuccess) return NotFound(ApiResponse<VendorDto>.FromResult(result));
        return Ok(ApiResponse<VendorDto>.FromResult(result, "Vendor reactivated."));
    }

    [HttpGet("{id:guid}/orders")]
    public async Task<ActionResult<ApiResponse<List<OrderDto>>>> GetOrders(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (!Owns(id)) return Forbid();
        var result = await _mediator.Send(new GetVendorOrdersQuery(id, page, pageSize), ct);
        if (!result.IsSuccess) return NotFound(ApiResponse<List<OrderDto>>.FromResult(result));
        return Ok(ApiResponse<List<OrderDto>>.FromResult(result));
    }

    public sealed record UpdateProfileRequest(string DisplayName);
    public sealed record UpdatePhoneRequest(string Phone);
    public sealed record UpdatePayoutRequest(string AccountNumber, string BankCode);
}
