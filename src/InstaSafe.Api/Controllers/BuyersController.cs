using InstaSafe.Application.Payments.Commands.InitiateBankAccountDebit;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstaSafe.Api.Controllers;

[ApiController]
[Route("api/buyers")]
public class BuyersController : ControllerBase
{
    private readonly IMediator _mediator;

    public BuyersController(IMediator mediator)
    {
        _mediator = mediator;
    }


    [HttpPost("orders/{orderId:guid}/bank-debit/initiate")]
    public async Task<IActionResult> InitiateBankAccountDebit(Guid orderId, [FromBody] InitiateBankDebitRequest request)
    {
        var command = new InitiateBankAccountDebitCommand(
            orderId, request.BuyerFirstName, request.BuyerLastName,
            request.BuyerEmail, request.BuyerPhone);
        var result = await _mediator.Send(command);
        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }
}


public record InitiateBankDebitRequest(
    string BuyerFirstName,
    string BuyerLastName,
    string BuyerEmail,
    string BuyerPhone
);
