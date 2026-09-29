using Serilog.Context;

namespace InstaSafe.Api.Middleware;

public class RequestCorrelationMiddleware
{
    private readonly RequestDelegate _next;

    public RequestCorrelationMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers.TryGetValue("X-Request-Id", out var values)
            ? values.ToString()
            : null;
        var requestId = string.IsNullOrWhiteSpace(incoming)
            ? Guid.NewGuid().ToString("N")[..12]
            : incoming;

        context.Response.Headers["X-Request-Id"] = requestId;

        using (LogContext.PushProperty("RequestId", requestId))
            await _next(context);
    }
}
