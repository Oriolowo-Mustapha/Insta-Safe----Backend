using InstaSafe.Application.Common.Helpers;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Dispatch.Commands.ConfirmDelivery;
using InstaSafe.Application.Features.Dispatch.Commands.RegisterDispatcher;
using InstaSafe.Application.Features.Dispatch.Commands.RequestDispatcherOtp;
using InstaSafe.Application.Features.Dispatch.Commands.VerifyDispatcherOtp;
using InstaSafe.Application.Features.Dispatch.DTOs;
using InstaSafe.Application.Features.Orders.DTOs;
using InstaSafe.Application.Common.Interfaces;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstaSafe.Api.Controllers;

[ApiController]
[Route("api/dispatch")]
public class DispatchController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IOrderRepository _orders;
    private readonly IMapper _mapper;

    public DispatchController(IMediator mediator, IOrderRepository orders, IMapper mapper)
    {
        _mediator = mediator; _orders = orders; _mapper = mapper;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<DispatcherDto>>> Register(
        [FromBody] RegisterDispatcherCommand cmd, CancellationToken ct)
    {
        var result = await _mediator.Send(cmd, ct);
        if (!result.IsSuccess) return BadRequest(ApiResponse<DispatcherDto>.FromResult(result));
        return Ok(ApiResponse<DispatcherDto>.FromResult(result, "Dispatcher registered."));
    }

    [HttpPost("request-code")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<bool>>> RequestCode(
        [FromBody] RequestCodeRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new RequestDispatcherOtpCommand(body.Phone), ct);
        if (!result.IsSuccess) return NotFound(ApiResponse<bool>.FromResult(result));
        return Ok(ApiResponse<bool>.FromResult(result, "Login code sent via WhatsApp."));
    }

    [HttpPost("verify-code")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<DispatcherAuthResponse>>> VerifyCode(
        [FromBody] VerifyCodeRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new VerifyDispatcherOtpCommand(body.Phone, body.Code), ct);
        if (!result.IsSuccess) return Unauthorized(ApiResponse<DispatcherAuthResponse>.FromResult(result));
        return Ok(ApiResponse<DispatcherAuthResponse>.FromResult(result, "Logged in."));
    }

    [HttpGet("assigned")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<OrderDto>>>> Assigned(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var driverId = User.DispatcherId();
        var phone = User.VendorPhone();
        if (driverId is null || phone is null) return Forbid();
        var orders = await _orders.ListByDriverAsync(driverId.Value, phone,
            page <= 0 ? 1 : page, Math.Clamp(pageSize, 1, 100), ct);
        return Ok(ApiResponse<List<OrderDto>>.SuccessResponse(_mapper.Map<List<OrderDto>>(orders)));
    }

    [HttpPost("orders/{id:guid}/confirm")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<OrderDto>>> Confirm(
        Guid id, [FromBody] ConfirmRequest body, CancellationToken ct)
    {
        var driverId = User.DispatcherId();
        var phone = User.VendorPhone();
        if (driverId is null || phone is null) return Forbid();
        var result = await _mediator.Send(
            new ConfirmDeliveryCommand(driverId.Value, phone, id, body.Otp), ct);
        if (!result.IsSuccess) return BadRequest(ApiResponse<OrderDto>.FromResult(result));
        return Ok(ApiResponse<OrderDto>.FromResult(result, "Delivery confirmed. Rider fee on its way."));
    }

    public sealed record RequestCodeRequest(string Phone);
    public sealed record VerifyCodeRequest(string Phone, string Code);
    public sealed record ConfirmRequest(string Otp);
}
