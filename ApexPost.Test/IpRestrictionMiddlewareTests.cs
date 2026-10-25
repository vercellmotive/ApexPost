using System.Net;
using ApexPost.Web.Attributes;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ApexPost.Test;

public sealed class IpRestrictionMiddlewareTests
{
    [Fact]
    public async Task RestrictedEndpoint_RejectsRemoteIpOutsideAllowlist()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.9");
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new IpRestrictedAttribute()),
            "staff"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:AllowedIps:0"] = "127.0.0.1"
            })
            .Build();
        var nextCalled = false;
        var middleware = new IpRestrictionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, configuration);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task UnmarkedEndpoint_IsNotBlockedByIpAllowlist()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.9");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:AllowedIps:0"] = "127.0.0.1"
            })
            .Build();
        var nextCalled = false;
        var middleware = new IpRestrictionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, configuration);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }
}
