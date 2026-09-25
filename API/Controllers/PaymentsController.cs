using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstaSafe.Api.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaystackClient _paystack;
    public PaymentsController(IPaystackClient paystack) => _paystack = paystack;

    /// <summary>Transfer-capable banks (name/slug/code) for dropdowns and DVA preferred_bank.</summary>
    [HttpGet("banks")]
    public async Task<ActionResult<ApiResponse<List<BankDto>>>> Banks(CancellationToken ct)
    {
        var banks = await _paystack.ListTransferBanksAsync(ct);
        return Ok(ApiResponse<List<BankDto>>.SuccessResponse(
            banks.Select(b => new BankDto(b.Name, b.Slug, b.Code)).ToList()));
    }

    public sealed record BankDto(string Name, string Slug, string Code);
}
