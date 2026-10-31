using System.Net;
using ApexPost.Web.Attributes;
using ApexPost.Web.Configuration;
using Microsoft.Extensions.Options;

namespace ApexPost.Web.Middleware;

public sealed class IpRestrictionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<SecurityOptions> _options;

    public IpRestrictionMiddleware(RequestDelegate next, IOptionsMonitor<SecurityOptions> options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<IpRestrictedAttribute>() is null)
        {
            await _next(context);
            return;
        }

        var allowedIps = _options.CurrentValue.AllowedIps;
        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp is null || !allowedIps.Any(ip => IPAddress.TryParse(ip, out var parsed) && parsed.Equals(remoteIp)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "IP address is not allowed." });
            return;
        }

        await _next(context);
    }
}
