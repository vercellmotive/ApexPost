using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Middleware;

// The one place that writes a Problem Details body, so every error leaves the pipeline in the same shape.
public static class ProblemResponse
{
    public static async Task WriteAsync(HttpContext context, int status, string title, string? detail = null)
    {
        context.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };
        problem.Extensions["traceId"] = context.Items["CorrelationId"] as string ?? context.TraceIdentifier;
        context.Response.ContentType = "application/problem+json";
        await JsonSerializer.SerializeAsync(context.Response.Body, problem, cancellationToken: context.RequestAborted);
    }
}
