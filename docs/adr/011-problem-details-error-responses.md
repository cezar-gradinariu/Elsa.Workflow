# ADR-011: ProblemDetails (RFC 7807 / RFC 9457) for API Error Responses

**Status:** Accepted  
**Date:** 2026-04-05

## Context

API consumers need a consistent, machine-readable error format across all failure modes — validation failures, domain rule violations, not-found scenarios, and unhandled server errors. Without a standard, each endpoint may return errors in different shapes, making client integration brittle.

RFC 7807 "Problem Details for HTTP APIs" (superseded and extended by RFC 9457) defines a standard JSON error envelope. ASP.NET Core 7+ supports this natively via `IProblemDetailsService` and `ProblemDetails` middleware.

## Decision

All API error responses are returned as `application/problem+json` using the **RFC 9457 ProblemDetails** shape:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation error",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/api/fulfilments/00000000-0000-0000-0000-000000000000",
  "errors": {
    "orderLines": ["OrderLines cannot be empty."]
  }
}
```

### HTTP status code mapping

| Scenario | HTTP status | ProblemDetails type |
|----------|-------------|---------------------|
| Input validation failure (request binding, data annotation) | 400 Bad Request | standard RFC 9110 |
| Domain rule violation (`DomainException`) | 422 Unprocessable Entity | `domain-error` |
| Aggregate / resource not found | 404 Not Found | standard RFC 9110 |
| Optimistic concurrency conflict | 409 Conflict | `concurrency-conflict` |
| Unhandled server error | 500 Internal Server Error | (internal detail hidden) |

### Implementation

**ASP.NET Core `ProblemDetails` middleware** is enabled globally in `Program.cs`:

```csharp
builder.Services.AddProblemDetails();
app.UseExceptionHandler();
app.UseStatusCodePages();
```

**Domain exception handler** (`Api/Middleware/DomainExceptionHandler.cs`) maps `DomainException` subclasses to 422 responses:

```csharp
public class DomainExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        if (exception is not DomainException domainEx)
            return false;

        var problemDetails = new ProblemDetails
        {
            Status   = StatusCodes.Status422UnprocessableEntity,
            Title    = "Domain rule violation",
            Detail   = domainEx.Message,
            Type     = "domain-error",
            Instance = httpContext.Request.Path
        };
        httpContext.Response.StatusCode = problemDetails.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, ct);
        return true;
    }
}
```

**Concurrency conflict handler** maps `OptimisticConcurrencyException` to 409.

### What NOT to do

- Do not return raw exception messages from unhandled server errors (information leakage).
- Do not mix ProblemDetails with non-standard error shapes on any endpoint.
- Do not use 400 for domain violations — 422 signals that the request was syntactically valid but semantically rejected.

## Consequences

**Positive:**
- Uniform error contract across all endpoints — clients parse one shape.
- `DomainException` → 422 clearly communicates that the request was received and understood but rejected by business rules.
- ASP.NET Core handles 4xx/5xx status codes from minimal APIs or controller actions automatically.

**Negative:**
- Requires discipline: all exception types must be mapped explicitly; an unmapped exception type falls through to 500.
- `type` URIs in ProblemDetails should ideally point to human-readable documentation — for custom types (e.g. `domain-error`) this means maintaining those pages or accepting relative/opaque type strings for v1.
