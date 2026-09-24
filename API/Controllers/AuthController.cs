using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Auth.Commands.RequestVendorOtp;
using InstaSafe.Application.Features.Auth.Commands.VerifyVendorOtp;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace InstaSafe.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    public AuthController(IMediator mediator) => _mediator = mediator;

    [HttpPost("vendor/request-code")]
    public async Task<ActionResult<ApiResponse<bool>>> RequestCode(
        [FromBody] RequestCodeRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new RequestVendorOtpCommand(body.Phone), ct);
        if (!result.IsSuccess) return NotFound(ApiResponse<bool>.FromResult(result));
        return Ok(ApiResponse<bool>.FromResult(result, "Login code sent via WhatsApp."));
    }

    [HttpPost("vendor/verify-code")]
    public async Task<ActionResult<ApiResponse<VendorAuthResponse>>> VerifyCode(
        [FromBody] VerifyCodeRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new VerifyVendorOtpCommand(body.Phone, body.Code), ct);
        if (!result.IsSuccess) return Unauthorized(ApiResponse<VendorAuthResponse>.FromResult(result));
        return Ok(ApiResponse<VendorAuthResponse>.FromResult(result, "Logged in."));
    }

    public sealed record RequestCodeRequest(string Phone);
    public sealed record VerifyCodeRequest(string Phone, string Code);
}
