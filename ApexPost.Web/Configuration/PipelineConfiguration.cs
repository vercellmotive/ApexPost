using System.Net;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.HttpOverrides;

namespace ApexPost.Web.Configuration;

public static class PipelineConfiguration
{
    // The order and the reason for each position are documented in DESIGN.md.
    public static WebApplication UseApexPostPipeline(this WebApplication app)
    {
        app.UseForwardedHeaders(CreateForwardedHeadersOptions(app.Configuration));

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseMiddleware<RequestCorrelationMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseMiddleware<RequestTimingMiddleware>();
        app.UseMiddleware<GlobalExceptionMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<MaintenanceModeMiddleware>();
        app.UseRequestLocalization(new RequestLocalizationOptions()
            .SetDefaultCulture("en-AU")
            .AddSupportedCultures("en-AU")
            .AddSupportedUICultures("en-AU"));
        app.UseResponseCompression();
        app.UseStatusCodePages();
        app.UseCookiePolicy();
        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseCors();
        app.UseMiddleware<IpRestrictionMiddleware>();
        app.UseRateLimiter();
        app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();
        return app;
    }

    public static WebApplication MapApexPostEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");
        return app;
    }

    private static ForwardedHeadersOptions CreateForwardedHeadersOptions(IConfiguration configuration)
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            RequireHeaderSymmetry = false,
            ForwardLimit = 1
        };
        foreach (var proxy in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
        {
            if (!IPAddress.TryParse(proxy, out var proxyAddress))
            {
                throw new InvalidOperationException($"ForwardedHeaders:KnownProxies contains invalid IP address '{proxy}'.");
            }

            options.KnownProxies.Add(proxyAddress);
        }

        return options;
    }
}
