# ADR-006: Circuit Breaker for External API Calls

**Status:** Accepted  
**Date:** 2026-04-03  
**Updated:** 2026-04-12

## Context

External API calls from workflow activities need a circuit breaker to stop hammering a failing dependency and fail fast while it recovers. The mechanism must integrate with Elsa's execution model and be observable via the Studio.

Three options were evaluated:

| Option | Description |
|--------|-------------|
| A | `Elsa.Resilience` — `IResilienceStrategy` + `IResilientActivityInvoker` at the activity level |
| B | Polly `ResiliencePipelineBuilder` on named `HttpClient` registrations in Infrastructure |
| C | Distributed circuit breaker via Redis / MongoDB shared state (multi-instance) |

## Decision

**Use Option A. Defer Option C to v2.**

Option B (Infrastructure-level) was the original design. It has been superseded by Option A because:
- Resilience configuration lives next to the activity code that needs it, not buried in DI registration.
- Retry history is first-class: recorded per activity instance by `IRetryAttemptRecorder` and surfaced in the Studio via `GET /elsa/api/resilience/retries/{activityInstanceId}`.
- A single Polly pipeline in the `IResilienceStrategy` eliminates the risk of double-retry that would occur if both the `HttpClient` handler and the activity independently retried.

**Option A — Elsa.Resilience (`IResilienceStrategy`):**

One `IResilienceStrategy` per external target, registered via `IResilienceStrategySource`:

```csharp
// src/Application/Resilience/OfaResilienceStrategy.cs
public class OfaResilienceStrategy : IResilienceStrategy
{
    public const string StrategyId = "ofa-resilience";

    public string Id          { get; set; } = StrategyId;
    public string DisplayName { get; set; } = "OFA Retry + Circuit Breaker";

    public Task ConfigurePipeline<T>(ResiliencePipelineBuilder<T> builder, ResilienceContext ctx)
    {
        builder
            .AddRetry(new RetryStrategyOptions<T>
            {
                ShouldHandle     = args => new ValueTask<bool>(IsTransient(args.Outcome.Exception)),
                MaxRetryAttempts = 3,
                Delay            = TimeSpan.FromSeconds(1),
                BackoffType      = DelayBackoffType.Exponential,
                UseJitter        = true,
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<T>
            {
                ShouldHandle      = args => new ValueTask<bool>(IsTransient(args.Outcome.Exception)),
                SamplingDuration  = TimeSpan.FromSeconds(30),
                FailureRatio      = 0.5,
                MinimumThroughput = 5,
                BreakDuration     = TimeSpan.FromSeconds(15),
            });
        return Task.CompletedTask;
    }

    private static bool IsTransient(Exception? ex) =>
        ex is HttpRequestException or TaskCanceledException or TimeoutException;
}
```

The named `HttpClient("OFA")` in Infrastructure carries only the base address and a hard per-request timeout. No `.AddStandardResilienceHandler()`:

```csharp
// src/Infrastructure/Extensions/ElsaInfrastructureExtensions.cs
services.AddHttpClient("OFA", client =>
{
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout     = TimeSpan.FromSeconds(30); // hard backstop only
});
```

The circuit breaker state is a singleton per Polly pipeline — shared across all workflow instances in the same process.

**Option C — deferred:** Acceptable for v1 single-instance. Per-process circuits still protect each instance from hammering a failing dependency. The fault-based Level-2 escalation (ADR-004) provides recovery even when the circuit resets on process restart.

## Consequences

**Positive:**
- Single Polly pipeline per external target — no double-retry risk.
- Retry history recorded per activity instance — observable in Studio and via REST API.
- `IResilienceStrategy` implementations are plain C# classes with no DI boilerplate.
- The Studio strategy picker lists registered strategies via `IResilienceStrategyCatalog`.

**Negative:**
- Circuit state is per-process — in a multi-instance deployment, one instance's circuit may be open while another's is closed. Accepted for v1.
- `Microsoft.Extensions.Http.Resilience` is still a dependency in `Api.csproj` (present before this ADR revision). It should be removed in the next housekeeping pass.
- Distributed circuit breaker (Option C) requires Redis or a custom MongoDB implementation — add to backlog for multi-instance milestone.
