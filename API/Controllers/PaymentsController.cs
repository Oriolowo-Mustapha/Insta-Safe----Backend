using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstaSafe.Api.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IPaystackClient _paystack;
    public PaymentsController(IPaystackClient paystack) => _paystack = paystack;

    /// <summary>
    /// Nigerian banks for dropdowns. Full list by default;
    /// transferOnly=true narrows to DVA-receivable banks (for preferred_bank).
    /// Public: needed on signup/onboarding screens before login.
    /// </summary>
    [HttpGet("banks")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<BankDto>>>> Banks(
        [FromQuery] bool transferOnly = false, CancellationToken ct = default)
    {
        var banks = transferOnly
            ? await _paystack.ListTransferBanksAsync(ct)
            : await _paystack.ListAllBanksAsync(ct);
        return Ok(ApiResponse<List<BankDto>>.SuccessResponse(
            banks.Select(b => new BankDto(b.Name, b.Slug, b.Code)).ToList()));
    }

    /// <summary>
    /// Verify an account number + bank code resolve to a real account name.
    /// Public: use live on the payout form so the vendor confirms the holder
    /// name matches before saving. Frontend compares with the typed name.
    /// </summary>
    [HttpGet("banks/resolve")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ResolveDto>>> Resolve(
        [FromQuery] string accountNumber, [FromQuery] string bankCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accountNumber) || string.IsNullOrWhiteSpace(bankCode))
            return BadRequest(ApiResponse<ResolveDto>.FailureResponse("accountNumber and bankCode are required."));
        var (ok, name, error) = await _paystack.ResolveAccountAsync(accountNumber.Trim(), bankCode.Trim(), ct);
        if (!ok) return BadRequest(ApiResponse<ResolveDto>.FailureResponse(error ?? "Account verification failed."));
        return Ok(ApiResponse<ResolveDto>.SuccessResponse(new ResolveDto(accountNumber.Trim(), bankCode.Trim(), name!)));
    }

    public sealed record BankDto(string Name, string Slug, string Code);
    public sealed record ResolveDto(string AccountNumber, string BankCode, string AccountName);
}
