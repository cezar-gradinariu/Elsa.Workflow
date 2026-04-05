using Elsa.Workflow.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Elsa.Workflow.Api.Middleware;

/// <summary>
/// Maps domain exceptions to RFC 9457 ProblemDetails responses (ADR-011).
/// Registered via app.AddExceptionHandler&lt;DomainExceptionHandler&gt;() in Program.cs.
/// </summary>
internal sealed class DomainExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext       httpContext,
        Exception         exception,
        CancellationToken ct)
    {
        var (status, type) = exception switch
        {
            FulfilmentOrderAlreadyExistsException => (StatusCodes.Status409Conflict,         "fulfilment-order-already-exists"),
            FulfilmentOrderNotFoundException      => (StatusCodes.Status404NotFound,          "fulfilment-order-not-found"),
            OptimisticConcurrencyException        => (StatusCodes.Status409Conflict,          "concurrency-conflict"),
            DomainException                       => (StatusCodes.Status422UnprocessableEntity, "domain-error"),
            _                                     => (0, string.Empty)
        };

        if (status == 0)
            return false;   // Not a domain exception — let ASP.NET Core's default handler produce 500.

        var problem = new ProblemDetails
        {
            Status   = status,
            Title    = ReasonForStatus(status),
            Detail   = exception.Message,
            Type     = type,
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode  = status;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }

    private static string ReasonForStatus(int status) => status switch
    {
        StatusCodes.Status404NotFound              => "Not Found",
        StatusCodes.Status409Conflict              => "Conflict",
        StatusCodes.Status422UnprocessableEntity   => "Domain Rule Violation",
        _                                          => "Error"
    };
}
