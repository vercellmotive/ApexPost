namespace ApexPost.Web.Middleware;

public sealed class MaintenanceModeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public MaintenanceModeMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path == "/health")
        {
            await _next(context);
            return;
        }

        var maintenanceMode = _configuration.GetValue<bool>("MaintenanceMode:Enabled");
        if (maintenanceMode)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers["Retry-After"] = "120";
            await context.Response.WriteAsJsonAsync(new { error = "ApexPost is temporarily in maintenance mode. Please retry shortly." });
            return;
        }

        await _next(context);
    }
}
