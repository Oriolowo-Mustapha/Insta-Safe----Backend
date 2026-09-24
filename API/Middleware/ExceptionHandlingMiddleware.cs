using FluentValidation;
using InstaSafe.Application.Common.Models;
using InstaSafe.Domain.Exceptions;
using System.Text.Json;

namespace InstaSafe.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next; _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        var (status, message, errors) = ex switch
        {
            ValidationException vex => (StatusCodes.Status400BadRequest, "Validation failed.",
                vex.Errors.Select(e => e.ErrorMessage).ToList()),
            NotFoundException => (StatusCodes.Status404NotFound, ex.Message, null),
            DomainValidationException => (StatusCodes.Status400BadRequest, ex.Message, null),
            ConflictException => (StatusCodes.Status409Conflict, ex.Message, null),
            ForbiddenAccessException => (StatusCodes.Status403Forbidden, ex.Message, null),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized.", null),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", null)
        };

        if (status == 500)
            _logger.LogError(ex, "Unhandled exception {TraceId}", context.TraceIdentifier);
        else
            _logger.LogWarning(ex, "Handled {Status}: {Message}", status, message);

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        var payload = ApiResponse<object>.FailureResponse(message, errors as List<string>);
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
