using InstaSafe.Application.Orders.Queries.GetMerchantOrders;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstaSafe.Api.Controllers;

[ApiController]
[Route("api/merchants")]
[Authorize]
public class MerchantsController : ControllerBase
{
    private readonly IMediator _mediator;

    public MerchantsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{merchantId:guid}/orders")]
    public async Task<IActionResult> GetMerchantOrders(
        Guid merchantId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? statusFilter = null)
    {
        var query = new GetMerchantOrdersQuery(merchantId, pageNumber, pageSize, statusFilter);
        var result = await _mediator.Send(query);
        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }
    [HttpPost("{merchantId:guid}/complete-profile")]
    public async Task<IActionResult> CompleteProfile(Guid merchantId, [FromBody] CompleteProfileRequest request, CancellationToken ct)
    {
        // Enforce that a user can only complete their own profile
        var authenticatedUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (merchantId.ToString() != authenticatedUserId)
            return Forbid();

        var command = new InstaSafe.Application.Merchants.Commands.CompleteProfile.CompleteProfileCommand(
            merchantId, request.Bvn, request.Nin, request.PayoutBankAccount, request.PayoutBankCode);
            
        var result = await _mediator.Send(command, ct);
        
        if (!result.Succeeded)
            return BadRequest(new { Message = string.Join("; ", result.Errors) });

        return Ok(new { Message = result.Data });
    }

    [HttpGet("banks")]
    [AllowAnonymous] // Or keep it authorized, but usually banks list is safe to expose
    public async Task<IActionResult> GetBanks(CancellationToken ct)
    {
        var query = new InstaSafe.Application.Merchants.Queries.GetBanks.GetBanksQuery();
        var result = await _mediator.Send(query, ct);

        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }
}

public record CompleteProfileRequest(string Bvn, string? Nin, string PayoutBankAccount, string PayoutBankCode);
