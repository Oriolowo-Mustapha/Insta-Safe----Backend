using InstaSafe.Application.Auth.Commands;
using InstaSafe.Application.Auth.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstaSafe.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var command = new RegisterCommand(request.FirstName, request.LastName, request.Email, request.Password, request.BusinessName, request.Phone, request.DateOfBirth);
        var result = await _mediator.Send(command, ct);

        if (!result.Succeeded)
            return BadRequest(new { Message = string.Join("; ", result.Errors) });

        return Ok(new { Message = result.Data });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var command = new LoginCommand(request.Email, request.Password);
        var result = await _mediator.Send(command, ct);

        if (!result.Succeeded)
            return Unauthorized(new { Message = string.Join("; ", result.Errors) });

        return Ok(new
        {
            Token = result.Data!.Token,
            UserId = result.Data.UserId,
            Email = result.Data.Email,
            FirstName = result.Data.FirstName,
            LastName = result.Data.LastName,
            Roles = result.Data.Roles,
            IsVerified = result.Data.IsVerified,
            BusinessName = result.Data.BusinessName
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken ct)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var query = new GetCurrentUserQuery(Guid.Parse(userId));
        var result = await _mediator.Send(query, ct);

        if (!result.Succeeded)
            return Unauthorized();

        return Ok(new
        {
            Token = result.Data!.Token,
            UserId = result.Data.UserId,
            Email = result.Data.Email,
            FirstName = result.Data.FirstName,
            LastName = result.Data.LastName,
            Roles = result.Data.Roles,
            IsVerified = result.Data.IsVerified,
            BusinessName = result.Data.BusinessName
        });
    }
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var command = new RefreshTokenCommand(request.Token, request.RefreshToken);
        var result = await _mediator.Send(command, ct);

        if (!result.Succeeded)
            return Unauthorized(new { Message = string.Join("; ", result.Errors) });

        return Ok(new
        {
            Token = result.Data!.Token,
            RefreshToken = result.Data.RefreshToken,
            UserId = result.Data.UserId,
            Email = result.Data.Email,
            FirstName = result.Data.FirstName,
            LastName = result.Data.LastName,
            Roles = result.Data.Roles,
            IsVerified = result.Data.IsVerified,
            BusinessName = result.Data.BusinessName
        });
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken ct)
    {
        var command = new VerifyEmailCommand(request.Email, request.Token);
        var result = await _mediator.Send(command, ct);

        if (!result.Succeeded)
            return BadRequest(new { Message = string.Join("; ", result.Errors) });

        return Ok(new { Message = result.Data });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        var command = new ForgotPasswordCommand(request.Email);
        var result = await _mediator.Send(command, ct);

        if (!result.Succeeded)
            return BadRequest(new { Message = string.Join("; ", result.Errors) });

        return Ok(new { Message = result.Data });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var command = new ResetPasswordCommand(request.Email, request.Token, request.NewPassword);
        var result = await _mediator.Send(command, ct);

        if (!result.Succeeded)
            return BadRequest(new { Message = string.Join("; ", result.Errors) });

        return Ok(new { Message = result.Data });
    }
}

public record RegisterRequest(string FirstName, string LastName, string Email, string Password, string BusinessName, string? Phone, DateTime DateOfBirth);
public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string Token, string RefreshToken);
public record VerifyEmailRequest(string Email, string Token);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string Token, string NewPassword);
