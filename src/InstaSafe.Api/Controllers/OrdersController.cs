using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Orders.Commands.CreateOrder;
using InstaSafe.Application.Orders.Commands.GenerateEscrowLink;
using InstaSafe.Application.Orders.Queries.GetOrderById;
using InstaSafe.Application.Orders.Queries.GetOrderTimeline;
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

    public OrdersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Succeeded
            ? CreatedAtAction(nameof(GetOrderById), new { orderId = result.Data!.OrderId }, result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    [HttpPost("{orderId:guid}/escrow-link")]
    public async Task<IActionResult> GenerateEscrowLink(Guid orderId, [FromBody] GenerateEscrowLinkRequest request)
    {
        var command = new GenerateEscrowLinkCommand(
            orderId, request.BuyerFirstName, request.BuyerLastName,
            request.BuyerEmail, request.BuyerPhone);
        var result = await _mediator.Send(command);
        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> GetOrderById(Guid orderId)
    {
        var query = new GetOrderByIdQuery(orderId);
        var result = await _mediator.Send(query);
        return result.Succeeded
            ? Ok(result.Data)
            : NotFound(new { errors = result.Errors });
    }

    [HttpGet("{orderId:guid}/timeline")]
    public async Task<IActionResult> GetOrderTimeline(Guid orderId)
    {
        var query = new GetOrderTimelineQuery(orderId);
        var result = await _mediator.Send(query);
        return result.Succeeded
            ? Ok(result.Data)
            : NotFound(new { errors = result.Errors });
    }


}

public record GenerateEscrowLinkRequest(
    string BuyerFirstName,
    string BuyerLastName,
    string BuyerEmail,
    string BuyerPhone
);
