# ADR-004: Two-Level Resilience Strategy for External Calls

**Status:** Accepted  
**Date:** 2026-04-03  
**Updated:** 2026-04-12

## Context

Workflow activities must call external HTTP APIs and message brokers. These calls can fail transiently (brief network blip) or for sustained periods (service outage, circuit open). A failed activity that faults the workflow is unacceptable — the workflow must recover automatically.

## Decision

Apply two independent resilience layers operating at different timescales:

| Level | Mechanism | Where | Timescale | Handles |
|-------|-----------|-------|-----------|---------|
| 1 — Fast | `Elsa.Resilience` (`IResilienceStrategy` + `IResilientActivityInvoker`) | Application (activity) | ms → seconds | Transient blips, brief 5xx, network hiccups |
| 2 — Durable | Elsa workflow reaches `Faulted`; operator resumes via Alterations API | Elsa runtime | minutes → hours | Level-1 exhausted, sustained outage |

Activities in `Application` call `IResilientActivityInvoker.InvokeAsync`, which wraps the action in the Polly pipeline configured by the activity's named `IResilienceStrategy`. The named `HttpClient` in `Infrastructure` carries only the base address and a hard timeout — no `.AddStandardResilienceHandler()`.

**Level 1 — Elsa.Resilience (Application)**

Each external-facing activity declares which `IResilienceStrategy` to use via its `CustomProperties["resilienceStrategy"]`. The `IResilientActivityInvoker` service resolves the strategy at execution time and runs the Polly pipeline transparently.

```csharp
public class CallOfaActivity : Activity, IResilientActivity
{
    public CallOfaActivity()
    {
        CustomProperties["resilienceStrategy"] = new ResilienceStrategyConfig
        {
            Mode       = ResilienceStrategyConfigMode.Identifier,
            StrategyId = OfaResilienceStrategy.StrategyId,
        }.SerializeToNode();
    }

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var invoker = context.WorkflowExecutionContext.ServiceProvider
            .GetRequiredService<IResilientActivityInvoker>();

        var result = await invoker.InvokeAsync<OfaAllocationResponse>(
            activity:          this,
            context:           context,
            action:            async () => /* HTTP call */,
            cancellationToken: context.CancellationToken);
    }

    public IDictionary<string, string?> CollectRetryDetails(
        ActivityExecutionContext context, RetryAttempt attempt)
        => new Dictionary<string, string?> { ["exception"] = attempt.Exception?.GetType().Name };
}
```

Each strategy implements `IResilienceStrategy` and configures a Polly pipeline (see [ADR-006](006-circuit-breaker.md)):

```csharp
public class OfaResilienceStrategy : IResilienceStrategy
{
    public string Id          { get; set; } = "ofa-resilience";
    public string DisplayName { get; set; } = "OFA Retry + Circuit Breaker";

    public Task ConfigurePipeline<T>(ResiliencePipelineBuilder<T> builder, ResilienceContext ctx)
    {
        builder
            .AddRetry(new RetryStrategyOptions<T> { MaxRetryAttempts = 3, /* … */ })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<T> { /* … */ });
        return Task.CompletedTask;
    }
}
```

Strategies are discovered via `IResilienceStrategySource` and listed in `IResilienceStrategyCatalog`. Retry history per activity instance is persisted by `IRetryAttemptRecorder` and queryable via `GET /elsa/api/resilience/retries/{activityInstanceId}`.

**Level 2 — Fault + Alterations API (current)**

When Polly exhausts all retries, the exception propagates from `IResilientActivityInvoker.InvokeAsync`. Elsa catches it and records the workflow as `Faulted`. An operator can then use the Elsa Alterations API to retry or skip the faulted activity.

This satisfies OQ-3 from ADR-013 pending a product decision. A fully durable Level-2 retry (bookmark suspend + scheduled resume) may be implemented in a future iteration once the retry policy (max attempts, delay schedule) is confirmed.

**Optional: uniform fault interception via `IIncidentStrategy`**

To intercept all activity faults at the framework level rather than per-activity try/catch:

```csharp
public class SuspendOnFaultIncidentStrategy : IIncidentStrategy
{
    public async ValueTask HandleAsync(ActivityExecutionContext context, Exception exception)
    {
        context.WorkflowExecutionContext.Incidents.Add(new ActivityIncident(/* … */));
        await context.SuspendAsync();
    }
}
// Registration: services.AddSingleton<IIncidentStrategy, SuspendOnFaultIncidentStrategy>();
```

## Consequences

**Positive:**
- Level-1 resilience is first-class in Elsa: retry history is visible in Studio, strategies are discoverable via the REST API, and the pipeline is configured alongside the activity rather than buried in Infrastructure DI wiring.
- No `.AddStandardResilienceHandler()` on `HttpClient` — single source of truth for retry policy, no risk of double-retry.
- Retry telemetry is recorded per activity instance via `IRetryAttemptRecorder`.

**Negative:**
- Activities that call external I/O must implement `IResilientActivity` and set `CustomProperties["resilienceStrategy"]` — a small contract requirement vs. plain `CodeActivity`.
- Level-2 durable retry (suspend + resume) is not yet implemented. Until it is, a sustained OFA outage eventually faults the workflow and requires operator action.
- Circuit breaker state is per-process (in-memory Polly). After a process restart the circuit resets — acceptable for v1 single-instance; needs a distributed circuit store for multi-instance (deferred).
