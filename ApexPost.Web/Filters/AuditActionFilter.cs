using System.Text.Json;
using ApexPost.Application.Interfaces;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Filters;

public sealed class AuditActionFilter : IAsyncActionFilter
{
    private readonly IAuditStore _auditStore;

    public AuditActionFilter(IAuditStore auditStore)
    {
        _auditStore = auditStore;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var start = DateTimeOffset.UtcNow;
        ActionExecutedContext? executed = null;
        try
        {
            executed = await next();
        }
        finally
        {
            var httpContext = context.HttpContext;
            var safeKeys = new HashSet<string>(
                ["controller", "action", "id", "transactionId", "branchCode", "counterId",
                    "productSku", "quantityDelta", "reasonCode"],
                StringComparer.OrdinalIgnoreCase);
            var routeValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var routeValue in context.RouteData.Values.Where(value => safeKeys.Contains(value.Key)))
            {
                if (TryGetSafeAuditValue(routeValue.Key, routeValue.Value, out var safeValue))
                {
                    routeValues[routeValue.Key] = safeValue;
                }
            }

            foreach (var actionArgument in context.ActionArguments.Where(argument => safeKeys.Contains(argument.Key)))
            {
                if (TryGetSafeAuditValue(actionArgument.Key, actionArgument.Value, out var safeValue))
                {
                    routeValues[actionArgument.Key] = safeValue;
                }
            }

            if (httpContext.Items["Audit:TransactionId"] is Guid transactionId)
            {
                routeValues["transactionId"] = transactionId.ToString("D");
            }

            var statusCode = GetStatusCode(executed, httpContext.Response.StatusCode);

            var entry = new AuditEntry(
                Guid.NewGuid(),
                httpContext.Items["CorrelationId"] as string ?? httpContext.TraceIdentifier,
                httpContext.User.FindFirst("StaffId")?.Value ?? "anonymous",
                httpContext.User.FindFirst("Role")?.Value ?? "anonymous",
                httpContext.User.FindFirst("BranchCode")?.Value ?? "unknown",
                context.Controller.GetType().Name,
                context.RouteData.Values["action"]?.ToString() ?? "unknown",
                JsonSerializer.Serialize(routeValues),
                statusCode,
                (long)(DateTimeOffset.UtcNow - start).TotalMilliseconds,
                DateTimeOffset.UtcNow);

            await _auditStore.AppendAsync(entry, CancellationToken.None);
        }
    }

    private static bool TryGetSafeAuditValue(string key, object? value, out string safeValue)
    {
        if (value is Guid guid)
        {
            safeValue = guid.ToString("D");
            return true;
        }

        if (key.Equals("quantityDelta", StringComparison.OrdinalIgnoreCase) && value is int quantity)
        {
            safeValue = quantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        if (key.Equals("reasonCode", StringComparison.OrdinalIgnoreCase) && value is Enum reasonCode)
        {
            safeValue = reasonCode.ToString();
            return true;
        }

        if (value is string text
            && text.Length <= 128
            && text.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
        {
            safeValue = text;
            return true;
        }

        safeValue = string.Empty;
        return false;
    }

    private static int GetStatusCode(ActionExecutedContext? executed, int responseStatusCode)
    {
        if (executed?.Exception is not null && !executed.ExceptionHandled)
        {
            return StatusCodes.Status500InternalServerError;
        }

        if (executed?.Result is Microsoft.AspNetCore.Mvc.Infrastructure.IStatusCodeActionResult statusResult
            && statusResult.StatusCode is int explicitStatusCode)
        {
            return explicitStatusCode;
        }

        return executed?.Result is Microsoft.AspNetCore.Mvc.RedirectResult
            or Microsoft.AspNetCore.Mvc.LocalRedirectResult
            or Microsoft.AspNetCore.Mvc.RedirectToActionResult
            or Microsoft.AspNetCore.Mvc.RedirectToRouteResult
                ? StatusCodes.Status302Found
                : responseStatusCode;
    }
}
