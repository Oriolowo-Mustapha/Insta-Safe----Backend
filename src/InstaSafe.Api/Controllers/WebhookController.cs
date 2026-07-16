using InstaSafe.Application.Payments.Commands.ProcessAlatPayWebhook;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace InstaSafe.Api.Controllers;

[ApiController]
[Route("api/webhooks/alatpay")]
public class WebhookController : ControllerBase
{
    private readonly IMediator _mediator;

    public WebhookController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> ReceiveWebhook()
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync();
        var signature = Request.Headers["x-alatpay-signature"].FirstOrDefault();

        var command = new ProcessAlatPayWebhookCommand(payload, signature);
        await _mediator.Send(command);

        return Ok(new { message = "Received" });
    }
}
