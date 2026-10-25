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
            AuthorizationFilterContext mvcContext =>
                mvcContext.RouteData.Values["branchCode"]?.ToString(),
            HttpContext httpContext =>
                httpContext.Request.RouteValues["branchCode"]?.ToString(),
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(assignedBranch)
            && (string.IsNullOrWhiteSpace(requestedBranch)
                || string.Equals(assignedBranch, requestedBranch, StringComparison.Ordinal)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
