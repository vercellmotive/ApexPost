using System.Security.Claims;
using ApexPost.Application.Interfaces;
using ApexPost.Application.Pricing;
using ApexPost.Application.Services;
using ApexPost.Domain.Common;
using ApexPost.Infrastructure.Services;
using ApexPost.Web.Attributes;
using ApexPost.Web.Authorization;
using ApexPost.Web.Filters;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.Filters.AddService<PerformanceActionFilter>();
    options.Filters.AddService<AuditActionFilter>();
});
builder.Services.AddScoped<ValidationActionFilter>();
builder.Services.AddScoped<PerformanceActionFilter>();
builder.Services.AddScoped<AuditActionFilter>();

builder.Services.AddAntiforgery();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Events.OnRedirectToAccessDenied = context =>
        {
            // A signed-in user without permission gets 403, not a redirect back to the login page.
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BranchManagerOnly", policy => policy.RequireClaim("Role", "BranchManager"));
    options.AddPolicy("AuditorOnly", policy => policy.RequireClaim("Role", "Auditor"));
    options.AddPolicy("AdministratorOnly", policy => policy.RequireClaim("Role", "Administrator"));
    options.AddPolicy("SameBranchOnly", policy => policy.AddRequirements(new SameBranchRequirement()));
});
builder.Services.AddSingleton<IAuthorizationHandler, SameBranchAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuditingAuthorizationResultHandler>();

builder.Services.AddHealthChecks();
builder.Services.AddMemoryCache();
builder.Services.AddResponseCompression();
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
    options.Secure = CookieSecurePolicy.Always;
    options.MinimumSameSitePolicy = SameSiteMode.Strict;
});
builder.Services.AddCors(options =>
{
    var partnerOrigin = builder.Configuration["Cors:PartnerTrackingWidgetOrigin"]
        ?? "https://tracking-widget.apexpost.example";
    options.AddPolicy("PartnerWidget", policy =>
    {
        policy.WithOrigins(partnerOrigin)
            .WithHeaders("X-Api-Key")
            .WithMethods(HttpMethods.Get);
    });
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("partner", context =>
    {
        var apiKeyValidator = context.RequestServices.GetRequiredService<PartnerApiKeyValidator>();
        var suppliedKey = context.Request.Headers["X-Api-Key"].FirstOrDefault()
            ?? context.Request.Headers["ApiKey"].FirstOrDefault();
        var partnerId = apiKeyValidator.FindPartnerId(suppliedKey);
        var partition = partnerId is not null
            ? $"partner:{partnerId}"
            : $"invalid:{context.Connection.RemoteIpAddress}";
        return RateLimitPartition.GetFixedWindowLimiter(
            partition,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsync("Request rate limit exceeded.", cancellationToken);
    };
});

builder.Services.AddJsonPersistence(builder.Environment.ContentRootPath);
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<PartnerApiKeyValidator>();
builder.Services.AddSingleton<INotificationService, SimulatedNotificationService>();
builder.Services.AddSingleton<IIdentityVerificationService, SimulatedIdentityVerificationService>();
builder.Services.AddSingleton(new TransactionRules(
    new ApexPost.Domain.ValueObjects.Money(
        builder.Configuration.GetValue<decimal>("Transactions:HighValueMoneyOrderThreshold", 1000m)),
    new ApexPost.Domain.ValueObjects.Money(
        builder.Configuration.GetValue<decimal>("Transactions:MaximumMoneyOrderAmount", 20000m))));
builder.Services.AddSingleton<IDomainEventDispatcher, DomainEventDispatcher>();
builder.Services.AddSingleton<ICourierRateService, SimulatedCourierRateService>();
builder.Services.AddSingleton<ICourierTrackingService>(_ => new SimulatedCourierTrackingService(
    TimeSpan.FromMilliseconds(builder.Configuration.GetValue<int>("CourierTracking:TimeoutMs", 1500)),
    TimeSpan.FromMilliseconds(builder.Configuration.GetValue<int>("CourierTracking:SimulatedDelayMs", 50))));

builder.Services.AddSingleton(provider =>
    new LowStockRule(provider.GetRequiredService<IConfiguration>().GetValue("Inventory:LowStockThreshold", 5)));
builder.Services.AddSingleton<ProductCatalogueService>();
builder.Services.AddSingleton<ICatalogueService>(sp => sp.GetRequiredService<ProductCatalogueService>());
builder.Services.AddSingleton<ICatalogueCache>(sp => sp.GetRequiredService<ProductCatalogueService>());

builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ReportingService>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<StaffAuthenticationService>();
builder.Services.AddSingleton<StaffManagementService>();
builder.Services.AddSingleton<BranchManagementService>();
builder.Services.AddSingleton<ProductManagementService>();
var parcelWeightSurcharge = builder.Configuration.GetValue<decimal>(
    "ShippingPricing:ParcelWeightSurchargePerKilogram",
    1.50m);
if (parcelWeightSurcharge < 0)
{
    throw new InvalidOperationException("ShippingPricing:ParcelWeightSurchargePerKilogram cannot be negative.");
}

builder.Services.AddSingleton<IPricingStrategy>(_ =>
    new TieredShippingPricingStrategy((details, zoneMultiplier) =>
        TieredShippingPricingStrategy.CalculateDefaultPrice(
            details,
            zoneMultiplier,
            parcelWeightSurcharge)));

var app = builder.Build();

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    RequireHeaderSymmetry = false,
    ForwardLimit = 1
};
foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
{
    if (!IPAddress.TryParse(proxy, out var proxyAddress))
    {
        throw new InvalidOperationException($"ForwardedHeaders:KnownProxies contains invalid IP address '{proxy}'.");
    }

    forwardedHeadersOptions.KnownProxies.Add(proxyAddress);
}
app.UseForwardedHeaders(forwardedHeadersOptions);

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

app.MapHealthChecks("/health");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program { }
