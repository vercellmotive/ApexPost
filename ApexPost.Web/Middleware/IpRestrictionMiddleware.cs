using System.Net;
using ApexPost.Web.Attributes;

namespace ApexPost.Web.Middleware;

public sealed class IpRestrictionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public IpRestrictionMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<IpRestrictedAttribute>() is null)
        {
            await _next(context);
            return;
        }

        var allowedIps = _configuration.GetSection("Security:AllowedIps").Get<string[]>() ?? [];
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
