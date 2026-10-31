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

        // OnStarting survives Response.Clear(), so error responses keep the header too.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Correlation-Id"] = correlationId;
            return Task.CompletedTask;
        });

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });

        await _next(context);
    }
}
