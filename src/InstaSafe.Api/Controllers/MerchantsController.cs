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
}
