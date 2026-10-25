using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Filters;

public sealed class ValidationActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.ModelState.IsValid)
        {
            if (context.HttpContext.Request.Method.Equals(HttpMethods.Get, StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new BadRequestObjectResult(context.ModelState);
                return;
            }

            context.Result = new BadRequestObjectResult(context.ModelState);
            return;
        }

        await next();
    }
}
