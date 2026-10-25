using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("Request {Method} {Path} was cancelled by the client.", context.Request.Method, context.Request.Path);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception processing {Method} {Path}.", context.Request.Method, context.Request.Path);
            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = "The request could not be completed. Use the trace ID when contacting support."
            };
            problem.Extensions["traceId"] = context.Items["CorrelationId"] as string ?? context.TraceIdentifier;
            context.Response.ContentType = "application/problem+json";
            await JsonSerializer.SerializeAsync(context.Response.Body, problem, cancellationToken: context.RequestAborted);
        }
    }
}
