using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ApexPost.Test;

public sealed class MaintenanceModeMiddlewareTests
{
    [Fact]
    public async Task MaintenanceModeReturns503AndRetryAfterForApplicationPages()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/Catalog";
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal("120", context.Response.Headers.RetryAfter);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task HealthEndpointRemainsAvailableDuringMaintenance()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/health";
        var middleware = CreateMiddleware(_ =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    private static MaintenanceModeMiddleware CreateMiddleware(RequestDelegate next)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MaintenanceMode:Enabled"] = "true"
            })
            .Build();
        return new MaintenanceModeMiddleware(next, configuration);
    }
}
