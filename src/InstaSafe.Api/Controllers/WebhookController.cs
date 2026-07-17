using InstaSafe.Application.Payments.Commands.ProcessMonnifyWebhook;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace InstaSafe.Api.Controllers;

[ApiController]
[Route("api/webhooks/monnify")]
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
        
        // Monnify uses 'monnify-signature' header for webhook validation
        var signature = Request.Headers["monnify-signature"].FirstOrDefault();

        // If signature is null, the validator in the command will catch it
        var command = new ProcessMonnifyWebhookCommand(payload, signature ?? string.Empty);
        var result = await _mediator.Send(command);

        if (!result.Succeeded)
        {
            // Returning Ok anyway is best practice for webhooks to avoid provider retries
            // if it's our internal logic error, but we log the error.
            return Ok(new { message = "Processed with warning/error", details = result.Errors });
        }

        return Ok(new { message = "Received" });
    }
}
