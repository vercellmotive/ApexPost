using System.Text.Json;
using ApexPost.Application.Interfaces;
using ApexPost.Web.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace ApexPost.Web.Authorization;

// Authorization rejects a request before MVC filters run, so denied business actions are audited here.
public sealed class AuditingAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();
    private readonly IAuditStore _auditStore;
    private readonly ILogger<AuditingAuthorizationResultHandler> _logger;

    public AuditingAuthorizationResultHandler(
        IAuditStore auditStore,
        ILogger<AuditingAuthorizationResultHandler> logger)
    {
        _auditStore = auditStore;
        _logger = logger;
    }

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        var endpoint = context.GetEndpoint();
        var audited = endpoint?.Metadata.GetMetadata<AuditedActionAttribute>();
        if (authorizeResult.Forbidden && audited is not null)
        {
            var action = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>();
            var entry = new AuditEntry(
                Guid.NewGuid(),
                context.Items["CorrelationId"] as string ?? context.TraceIdentifier,
                context.User.FindFirst("StaffId")?.Value ?? "anonymous",
                context.User.FindFirst("Role")?.Value ?? "anonymous",
                context.User.FindFirst("BranchCode")?.Value ?? "unknown",
                action is null ? "unknown" : $"{action.ControllerName}Controller",
                action?.ActionName ?? "unknown",
                JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["businessAction"] = audited.BusinessAction,
                    ["outcome"] = "AccessDenied"
                }),
                StatusCodes.Status403Forbidden,
                0,
                DateTimeOffset.UtcNow);
            try
            {
                await _auditStore.AppendAsync(entry, CancellationToken.None);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Audit write failed for a denied {BusinessAction}.", audited.BusinessAction);
            }
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
