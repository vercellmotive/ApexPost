using ApexPost.Web.Attributes;

namespace ApexPost.Web.Middleware;

public sealed class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly PartnerApiKeyValidator _apiKeyValidator;

    public ApiKeyAuthenticationMiddleware(RequestDelegate next, PartnerApiKeyValidator apiKeyValidator)
    {
        _next = next;
        _apiKeyValidator = apiKeyValidator;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var requiresApiKey = endpoint?.Metadata.GetMetadata<ApiKeyRequiredAttribute>() is not null;
        if (!requiresApiKey)
        {
            await _next(context);
            return;
        }

        if (!_apiKeyValidator.HasConfiguredKeys)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { error = "Integration API key is not configured." });
            return;
        }

        var providedKey = context.Request.Headers["X-Api-Key"].FirstOrDefault()
            ?? context.Request.Headers["ApiKey"].FirstOrDefault();

        var partnerId = _apiKeyValidator.FindPartnerId(providedKey);
        if (partnerId is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = string.IsNullOrWhiteSpace(providedKey) ? "API key is required." : "Invalid API key." });
            return;
        }

        context.Items["PartnerId"] = partnerId;
        await _next(context);
    }
}
