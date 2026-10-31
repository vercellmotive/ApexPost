using System.Globalization;
using ApexPost.Web.Configuration;
using Microsoft.Extensions.Options;

namespace ApexPost.Web.Middleware;

public sealed class MaintenanceModeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<MaintenanceOptions> _options;

    public MaintenanceModeMiddleware(RequestDelegate next, IOptionsMonitor<MaintenanceOptions> options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var options = _options.CurrentValue;
        if (options.Enabled && context.Request.Path != "/health")
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.RetryAfter = options.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            await context.Response.WriteAsJsonAsync(new { error = "ApexPost is temporarily in maintenance mode. Please retry shortly." });
            return;
        }

        await _next(context);
    }
}
