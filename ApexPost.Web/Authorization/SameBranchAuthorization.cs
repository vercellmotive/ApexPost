using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Authorization;

public sealed class SameBranchRequirement : IAuthorizationRequirement
{
}

public sealed class SameBranchAuthorizationHandler : AuthorizationHandler<SameBranchRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SameBranchRequirement requirement)
    {
        var assignedBranch = context.User.FindFirstValue("BranchCode");
        var requestedBranch = context.Resource switch
        {
            AuthorizationFilterContext mvcContext => GetRequestedBranch(mvcContext.HttpContext),
            HttpContext httpContext => GetRequestedBranch(httpContext),
            _ => null
        };

        // Auditors have a cross-branch remit; every other role may only read its assigned branch.
        if (!string.IsNullOrWhiteSpace(assignedBranch)
            && (string.IsNullOrWhiteSpace(requestedBranch)
                || context.User.HasClaim("Role", "Auditor")
                || string.Equals(assignedBranch, requestedBranch, StringComparison.Ordinal)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static string? GetRequestedBranch(HttpContext context) =>
        context.Request.RouteValues["branchCode"]?.ToString()
        ?? context.Request.Query["branchCode"].FirstOrDefault();
}
