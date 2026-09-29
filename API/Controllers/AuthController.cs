using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Auth.Commands.RequestEmailCode;
using InstaSafe.Application.Features.Auth.Commands.RequestVendorOtp;
using InstaSafe.Application.Features.Auth.Commands.VendorLogin;
using InstaSafe.Application.Features.Auth.Commands.VerifyEmailCode;
using InstaSafe.Application.Features.Auth.Commands.VerifyVendorOtp;
using InstaSafe.Application.Features.Vendors.DTOs;
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

    /// <summary>
    /// Primary vendor login (web dashboard): phone number OR email + password.
    /// Phone-OTP endpoints remain as fallback/recovery.
    /// </summary>
    [HttpPost("vendor/login")]
    public async Task<ActionResult<ApiResponse<VendorAuthResponse>>> Login(
        [FromBody] LoginRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new VendorLoginCommand(body.LoginId, body.Password), ct);
        if (!result.IsSuccess) return Unauthorized(ApiResponse<VendorAuthResponse>.FromResult(result));
        return Ok(ApiResponse<VendorAuthResponse>.FromResult(result, "Logged in."));
    }

    /// <summary>Step 1b of signup: (re)send the 6-digit email verification code.</summary>
    [HttpPost("vendor/request-email-code")]
    public async Task<ActionResult<ApiResponse<bool>>> RequestEmailCode(
        [FromBody] RequestEmailCodeRequestBody body, CancellationToken ct)
    {
        var result = await _mediator.Send(new RequestEmailCodeCommand(body.Email), ct);
        if (!result.IsSuccess) return BadRequest(ApiResponse<bool>.FromResult(result));
        return Ok(ApiResponse<bool>.FromResult(result, "Verification code sent to email."));
    }

    /// <summary>Step 1c of signup: confirm the email code.</summary>
    [HttpPost("vendor/verify-email")]
    public async Task<ActionResult<ApiResponse<VendorDto>>> VerifyEmail(
        [FromBody] VerifyEmailRequestBody body, CancellationToken ct)
    {
        var result = await _mediator.Send(new VerifyEmailCodeCommand(body.Email, body.Code), ct);
        if (!result.IsSuccess) return BadRequest(ApiResponse<VendorDto>.FromResult(result));
        return Ok(ApiResponse<VendorDto>.FromResult(result, "Email verified. Complete payout setup to finish onboarding."));
    }

    public sealed record RequestCodeRequest(string Phone);
    public sealed record VerifyCodeRequest(string Phone, string Code);
    public sealed record LoginRequest(string LoginId, string Password);
    public sealed record RequestEmailCodeRequestBody(string Email);
    public sealed record VerifyEmailRequestBody(string Email, string Code);
}
