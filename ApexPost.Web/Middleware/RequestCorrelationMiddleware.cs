namespace ApexPost.Web.Middleware;

public sealed class RequestCorrelationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestCorrelationMiddleware> _logger;

    public RequestCorrelationMiddleware(RequestDelegate next, ILogger<RequestCorrelationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var suppliedId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        var correlationId = !string.IsNullOrWhiteSpace(suppliedId)
            && suppliedId.Length <= 128
            && suppliedId.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
                ? suppliedId
                : Guid.NewGuid().ToString("N");
        context.TraceIdentifier = correlationId;
        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers["X-Correlation-Id"] = correlationId;

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["Path"] = context.Request.Path.Value ?? string.Empty,
            ["Method"] = context.Request.Method
        });

        _logger.LogInformation("Request started: {Method} {Path}", context.Request.Method, context.Request.Path.Value);
        await _next(context);
        _logger.LogInformation("Request completed: {Method} {Path} with status {StatusCode}", context.Request.Method, context.Request.Path.Value, context.Response.StatusCode);
    }
}
