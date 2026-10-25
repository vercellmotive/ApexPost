namespace ApexPost.Web.Middleware;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        finally
        {
            _logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} with correlation {CorrelationId}.",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                context.Items["CorrelationId"] as string ?? context.TraceIdentifier);
        }
    }
}
