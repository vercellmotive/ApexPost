using System.Diagnostics;
using ApexPost.Web.Configuration;
using Microsoft.Extensions.Options;

namespace ApexPost.Web.Middleware;

public sealed class RequestTimingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTimingMiddleware> _logger;
    private readonly IOptionsMonitor<PerformanceOptions> _options;

    public RequestTimingMiddleware(
        RequestDelegate next,
        ILogger<RequestTimingMiddleware> logger,
        IOptionsMonitor<PerformanceOptions> options)
    {
        _next = next;
        _logger = logger;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var durationMs = stopwatch.ElapsedMilliseconds;
            _logger.LogInformation("Request {Method} {Path} completed in {DurationMs} ms with status {StatusCode}", context.Request.Method, context.Request.Path.Value ?? "/", durationMs, context.Response.StatusCode);

            if (durationMs > _options.CurrentValue.SlowRequestThresholdMs)
            {
                _logger.LogWarning("Slow request exceeded threshold: {Method} {Path} took {DurationMs} ms", context.Request.Method, context.Request.Path.Value ?? "/", durationMs);
            }
        }
    }
}
