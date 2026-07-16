using InstaSafe.Application.Delivery.Commands.ConfirmDeliveryScan;
using InstaSafe.Application.Delivery.Commands.CreatePickupSession;
using InstaSafe.Application.Delivery.Commands.GenerateDeliveryQrCodes;
using InstaSafe.Application.Delivery.Queries.GetDeliverySessionStatus;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstaSafe.Api.Controllers;

[ApiController]
[Route("api/delivery-sessions")]
[Authorize]
public class DeliveryController : ControllerBase
{
    private readonly IMediator _mediator;

    public DeliveryController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("{orderId:guid}/qr-codes")]
    public async Task<IActionResult> GenerateQrCodes(Guid orderId)
    {
        var command = new GenerateDeliveryQrCodesCommand(orderId);
        var result = await _mediator.Send(command);
        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    [HttpPost("{orderId:guid}/pickup")]
    public async Task<IActionResult> CreatePickupSession(Guid orderId, [FromBody] CreatePickupSessionRequest request)
    {
        var command = new CreatePickupSessionCommand(
            orderId, request.MerchantQrToken, request.DeviceFingerprint,
            request.Latitude, request.Longitude);
        var result = await _mediator.Send(command);
        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    [HttpPost("{orderId:guid}/deliver")]
    public async Task<IActionResult> ConfirmDelivery(Guid orderId, [FromBody] ConfirmDeliveryRequest request)
    {
        var command = new ConfirmDeliveryScanCommand(
            orderId, request.SessionId, request.BuyerQrToken,
            request.DeviceFingerprint, request.Latitude, request.Longitude);
        var result = await _mediator.Send(command);
        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    [HttpGet("sessions/{sessionId:guid}")]
    public async Task<IActionResult> GetDeliverySessionStatus(Guid sessionId)
    {
        var query = new GetDeliverySessionStatusQuery(sessionId);
        var result = await _mediator.Send(query);
        return result.Succeeded
            ? Ok(result.Data)
            : NotFound(new { errors = result.Errors });
    }
}

public record CreatePickupSessionRequest(
    string MerchantQrToken,
    string DeviceFingerprint,
    double? Latitude,
    double? Longitude
);

public record ConfirmDeliveryRequest(
    Guid SessionId,
    string BuyerQrToken,
    string DeviceFingerprint,
    double? Latitude,
    double? Longitude
);
