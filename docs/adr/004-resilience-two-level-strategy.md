# ADR-004: Two-Level Resilience Strategy for External Calls

**Status:** Accepted  
**Date:** 2026-04-03

## Context

Workflow activities must call external HTTP APIs and message brokers. These calls can fail transiently (brief network blip) or for sustained periods (service outage, circuit open). Elsa has no built-in retry or fault-recovery mechanism at the activity level beyond recording an `Incident`. A failed activity that faults the workflow is unacceptable — the workflow must recover automatically.

## Decision

Apply two independent resilience layers operating at different timescales:

| Level | Mechanism | Where | Timescale | Handles |
|-------|-----------|-------|-----------|---------|
| 1 — Fast | Polly retry + circuit breaker | Infrastructure | ms → seconds | Transient blips, brief 5xx, network hiccups |
| 2 — Durable | Elsa bookmark suspend + scheduled resume | Application (activity) | minutes → hours | Sustained outage, Polly exhausted, circuit open |

Activities in `Application` never call `HttpClient` or broker SDKs directly — they call an injected interface; `Infrastructure` owns all Polly wiring. See [ADR-001](001-ddd-layer-structure.md) for the dependency rule.

**Level 1 — Polly (Infrastructure)**

HTTP calls use `Microsoft.Extensions.Http.Resilience` on named `HttpClient` registrations. Non-HTTP calls use `ResiliencePipelineBuilder` directly. See [ADR-006](006-circuit-breaker.md) for circuit breaker specifics.

**Level 2 — Elsa Durable Retry (Application)**

When Polly exhausts all retries, the exception reaches the activity. The activity catches it, increments a retry counter stored in workflow variables, creates a delayed bookmark, and suspends. The workflow state persists to MongoDB. After the delay the workflow resumes and Polly runs again from scratch.

```csharp
public class CallPaymentApiActivity : Activity
{
    private readonly IPaymentGateway _gateway;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var attempt = context.GetVariable<int>("_retryAttempt");

        try
        {
            var result = await _gateway.ChargeAsync(/* ... */);
            context.SetVariable("_retryAttempt", 0);
            context.SetOutput("Result", result);
        }
        catch (Exception ex) when (attempt < MaxDurableRetries)
        {
            var delay = TimeSpan.FromMinutes(Math.Pow(2, attempt)); // 1m, 2m, 4m…
            context.SetVariable("_retryAttempt", attempt + 1);

            context.CreateBookmark(new CreateBookmarkArgs
            {
                BookmarkName = "DurableRetry",
                Payload      = context.ActivityId,
                Callback     = ResumeAsync,
                AutoComplete = false,
            });

            await context.ScheduleDelayAsync(delay, context.CancellationToken);
        }
        catch (Exception ex)
        {
            // MaxDurableRetries exceeded — fault visibly for operator intervention
            await context.ScheduleFaultActivityAsync(ex);
        }
    }

    private async ValueTask ResumeAsync(ActivityExecutionContext context)
        => await ExecuteAsync(context);

    private const int MaxDurableRetries = 5;
}
```

**Optional: uniform fault interception via `IIncidentStrategy`**

To intercept all activity faults at the framework level rather than per-activity try/catch:

```csharp
public class SuspendOnFaultIncidentStrategy : IIncidentStrategy
{
    public async ValueTask HandleAsync(ActivityExecutionContext context, Exception exception)
    {
        context.WorkflowExecutionContext.Incidents.Add(new ActivityIncident(/* ... */));
        await context.SuspendAsync();
    }
}
// Registration: services.AddSingleton<IIncidentStrategy, SuspendOnFaultIncidentStrategy>();
```

## Consequences

**Positive:**
- Workflows are self-healing for both transient and sustained failures — no operator intervention needed until `MaxDurableRetries` is exceeded.
- Durable retry state survives process restarts — stored in MongoDB.
- When durable retries exhaust, the workflow reaches `Faulted` state where an operator can use the Alterations API to retry or skip the activity.
- Infrastructure resilience is fully decoupled from Application activity code.

**Negative:**
- Each activity that calls external I/O needs the try/catch durable-retry pattern — boilerplate unless `IIncidentStrategy` is used uniformly.
- The circuit breaker state is per-process (in-memory). After a durable suspend + resume, a restarted process resets the circuit — acceptable for v1 single-instance; needs a distributed circuit store for multi-instance (deferred).
- `MaxDurableRetries` is a constant — making it configurable per-activity type is future work.
