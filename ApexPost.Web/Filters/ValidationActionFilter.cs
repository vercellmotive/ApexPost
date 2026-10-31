using ApexPost.Web.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Filters;

public sealed class ValidationActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ModelState.IsValid)
        {
            await next();
            return;
        }

        var redisplay = context.ActionDescriptor.EndpointMetadata.OfType<RedisplayOnInvalidAttribute>().FirstOrDefault();
        var model = context.ActionArguments.Values.FirstOrDefault(argument =>
            argument is not (null or string or ValueType));
        if (redisplay is null || model is null || context.Controller is not Controller controller)
        {
            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState));
            return;
        }

        if (controller is IRepopulatesForm form)
        {
            await form.RepopulateFormAsync(model, context.HttpContext.RequestAborted);
        }

        context.Result = controller.View(redisplay.ViewName, model);
    }
}
