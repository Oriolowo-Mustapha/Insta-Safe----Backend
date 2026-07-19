using InstaSafe.Application.Disputes.Commands.RaiseDispute;
using InstaSafe.Application.Disputes.Commands.ResolveDispute;
using InstaSafe.Application.Disputes.Queries.GetAllDisputes;
using InstaSafe.Application.Disputes.Queries.GetDispute;
using InstaSafe.Application.Disputes.Queries.GetOrderDisputes;
using InstaSafe.Application.Payouts.Commands.ExecuteSplitPayout;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstaSafe.Api.Controllers;

[ApiController]
[Route("api/disputes")]
[Authorize]
public class DisputesController : ControllerBase
{
    private readonly IMediator _mediator;

    public DisputesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Get all disputes (admin list view).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAllDisputes()
    {
        var query = new GetAllDisputesQuery();
        var result = await _mediator.Send(query);
        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Buyer raises a dispute on a delivered order.</summary>
    [HttpPost]
    public async Task<IActionResult> RaiseDispute([FromBody] RaiseDisputeCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Succeeded
            ? CreatedAtAction(nameof(GetDispute), new { disputeId = result.Data!.DisputeId }, result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Admin resolves a dispute (refund or release).</summary>
    [HttpPut("{disputeId:guid}/resolve")]
    public async Task<IActionResult> ResolveDispute(Guid disputeId, [FromBody] ResolveDisputeRequest request)
    {
        // TODO: Extract ResolvedByUserId from claims in production
        var command = new ResolveDisputeCommand(disputeId, request.Resolution, request.AdminNotes, request.ResolvedByUserId);
        var result = await _mediator.Send(command);
        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Get a specific dispute by ID.</summary>
    [HttpGet("{disputeId:guid}")]
    public async Task<IActionResult> GetDispute(Guid disputeId)
    {
        var query = new GetDisputeQuery(disputeId);
        var result = await _mediator.Send(query);
        return result.Succeeded
            ? Ok(result.Data)
            : NotFound(new { errors = result.Errors });
    }

    /// <summary>Get the dispute for a specific order.</summary>
    [HttpGet("order/{orderId:guid}")]
    public async Task<IActionResult> GetOrderDispute(Guid orderId)
    {
        var query = new GetOrderDisputesQuery(orderId);
        var result = await _mediator.Send(query);
        return result.Succeeded
            ? Ok(result.Data)
            : NotFound(new { errors = result.Errors });
    }

    /// <summary>Execute the payout split for a completed order.</summary>
    [HttpPost("order/{orderId:guid}/payout")]
    public async Task<IActionResult> ExecutePayout(Guid orderId)
    {
        var command = new ExecuteSplitPayoutCommand(orderId);
        var result = await _mediator.Send(command);
        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }
}

public record ResolveDisputeRequest(string Resolution, string? AdminNotes, string ResolvedByUserId);
