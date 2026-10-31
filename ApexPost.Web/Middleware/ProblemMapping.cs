using ApexPost.Application.Interfaces;
using ApexPost.Domain.Common;

namespace ApexPost.Web.Middleware;

// EF Core exceptions are translated by the unit of work, so the web layer only knows Application types.
public static class ProblemMapping
{
    public static (int Status, string Title, string? Detail) For(Exception exception) => exception switch
    {
        ConcurrencyConflictException conflict => (StatusCodes.Status409Conflict, "The record was changed by someone else.", conflict.Message),
        DuplicateValueException duplicate => (StatusCodes.Status409Conflict, "The value is already in use.", duplicate.Message),
        KeyNotFoundException => (StatusCodes.Status404NotFound, "The record was not found.", null),
        DomainValidationException validation => (StatusCodes.Status400BadRequest, "The request broke a business rule.", validation.Message),
        UnauthorizedDomainActionException => (StatusCodes.Status403Forbidden, "This action is not allowed.", null),
        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", "The request could not be completed. Use the trace ID when contacting support.")
    };
}
