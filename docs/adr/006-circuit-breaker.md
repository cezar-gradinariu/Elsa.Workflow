# ADR-006: Circuit Breaker for External API Calls

**Status:** Accepted  
**Date:** 2026-04-03

## Context

Elsa 3 has no built-in circuit breaker. Its `SetResilienceStrategy` covers basic retry (count + interval) only. External API calls from workflow activities need a circuit breaker to stop hammering a failing dependency and to fail fast while it recovers, rather than exhausting all retry attempts on every request during an outage.

Three options were evaluated:

| Option | Description |
|--------|-------------|
| A | Polly via `Microsoft.Extensions.Http.Resilience` on named `HttpClient` (HTTP calls) |
| B | Polly `ResiliencePipeline` named registrations (non-HTTP: broker SDK, gRPC, etc.) |
| C | Distributed circuit breaker via Redis / MongoDB shared state (multi-instance) |

## Decision

**Use Option A for HTTP calls and Option B for non-HTTP calls. Defer Option C to v2.**

Both options use the same `Polly` + `Microsoft.Extensions.Http.Resilience` packages — no additional dependencies.

**Option A — HTTP (`Microsoft.Extensions.Http.Resilience`):**

One named `HttpClient` per external API target, each decorated with `.AddStandardResilienceHandler()`:

```csharp
services.AddHttpClient("ExternalPaymentApi", client =>
    {
        client.BaseAddress = new Uri(config["ExternalApis:Payment:BaseUrl"]!);
    })
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts  = 3;
        options.Retry.BackoffType       = DelayBackoffType.Exponential;
        options.Retry.UseJitter         = true;

        options.CircuitBreaker.SamplingDuration  = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.FailureRatio      = 0.5;
        options.CircuitBreaker.MinimumThroughput = 5;
        options.CircuitBreaker.BreakDuration     = TimeSpan.FromSeconds(15);
    });
```

The circuit breaker state is a singleton per named client — shared across all requests and workflow activities in the same process.

**Option B — Non-HTTP (`ResiliencePipeline`):**

```csharp
services.AddResiliencePipeline("payment-broker", builder =>
{
    builder
        .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 3, /* ... */ })
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            SamplingDuration  = TimeSpan.FromSeconds(30),
            FailureRatio      = 0.5,
            MinimumThroughput = 5,
            BreakDuration     = TimeSpan.FromSeconds(15),
        })
        .AddTimeout(TimeSpan.FromSeconds(10));
});
// Resolve via ResiliencePipelineProvider<string> injected into the Infrastructure service
```

**Option C — deferred:** When multiple API instances are deployed, each process has its own independent circuit state. A shared circuit breaker (Redis-backed or MongoDB-backed `ICircuitBreakerStorage`) would be needed. This is acceptable to defer because:
- v1 is single-instance.
- Independent per-process circuits still protect each instance from hammering a failing dependency.
- The durable Elsa retry (Level 2 — see [ADR-004](004-resilience-two-level-strategy.md)) provides recovery even when the circuit resets on process restart.

## Consequences

**Positive:**
- Circuit breaker state is managed automatically by Polly — no custom state management.
- Named `HttpClient` registrations centralise all resilience configuration in Infrastructure; activities are blind to it.
- No new packages — `Microsoft.Extensions.Http.Resilience` and `Polly` are already in the Infrastructure package list.

**Negative:**
- Circuit state is per-process — in a multi-instance deployment, one instance's circuit may be open while another's is closed. Accepted for v1.
- After a durable suspend + resume across a process restart, the circuit resets to closed. This is intentional — the external API may have recovered during the suspension window.
- Distributed circuit breaker (Option C) requires Redis or a custom MongoDB implementation — add to backlog for multi-instance milestone.
