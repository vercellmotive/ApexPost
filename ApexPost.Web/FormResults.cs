using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web;

// A form shown again because of an error answers 400, so clients, tests and the audit log see the failure
// while the clerk still keeps what they typed.
internal static class FormResults
{
    public static ViewResult BadRequestView(this Controller controller, string? viewName, object model)
    {
        var result = controller.View(viewName, model);
        result.StatusCode = StatusCodes.Status400BadRequest;
        return result;
    }
}
