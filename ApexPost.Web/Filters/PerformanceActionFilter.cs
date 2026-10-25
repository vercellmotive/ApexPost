using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Filters;

public sealed class PerformanceActionFilter : IAsyncActionFilter
{
    private readonly ILogger<PerformanceActionFilter> _logger;
    private readonly int _thresholdMs;

    public PerformanceActionFilter(ILogger<PerformanceActionFilter> logger, IConfiguration configuration)
    {
        _logger = logger;
        _thresholdMs = configuration.GetValue<int>("Performance:SlowActionThresholdMs", 500);
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next();
        }
        finally
        {
            stopwatch.Stop();
            if (stopwatch.ElapsedMilliseconds > _thresholdMs)
            {
                _logger.LogWarning(
                    "Slow action {Controller}.{Action} took {ElapsedMs} ms.",
                    context.Controller.GetType().Name,
                    context.ActionDescriptor.RouteValues["action"] ?? "unknown",
                    stopwatch.ElapsedMilliseconds);
            }
        }
    }
}
