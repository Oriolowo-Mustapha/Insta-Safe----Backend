using InstaSafe.Application.Payments.Commands.AuthenticateCardPayment;
using InstaSafe.Application.Payments.Commands.InitiateBankAccountDebit;
using InstaSafe.Application.Payments.Commands.InitiateCardPayment;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstaSafe.Api.Controllers;

[ApiController]
[Route("api/buyers")]
[Authorize]
public class BuyersController : ControllerBase
{
    private readonly IMediator _mediator;

    public BuyersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("orders/{orderId:guid}/card/initiate")]
    public async Task<IActionResult> InitiateCardPayment(Guid orderId, [FromBody] InitiateCardPaymentRequest request)
    {
        var command = new InitiateCardPaymentCommand(
            orderId, request.BuyerFirstName, request.BuyerLastName,
            request.BuyerEmail, request.BuyerPhone);
        var result = await _mediator.Send(command);
        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    [HttpPost("orders/{orderId:guid}/card/authenticate")]
    public async Task<IActionResult> AuthenticateCardPayment(Guid orderId, [FromBody] AuthenticateCardPaymentRequest request)
    {
        var command = new AuthenticateCardPaymentCommand(request.TransactionId, request.Otp);
        var result = await _mediator.Send(command);
        return result.Succeeded
            ? Ok(new { message = result.Data })
            : BadRequest(new { errors = result.Errors });
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

public record InitiateCardPaymentRequest(
    string BuyerFirstName,
    string BuyerLastName,
    string BuyerEmail,
    string BuyerPhone
);

public record AuthenticateCardPaymentRequest(
    string TransactionId,
    string Otp
);

public record InitiateBankDebitRequest(
    string BuyerFirstName,
    string BuyerLastName,
    string BuyerEmail,
    string BuyerPhone
);
