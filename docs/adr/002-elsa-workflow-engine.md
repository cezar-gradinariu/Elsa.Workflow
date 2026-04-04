# ADR-002: Elsa Workflows as the Embedded Orchestration Engine

**Status:** Accepted  
**Date:** 2026-04-03

## Context

The Application layer requires durable process orchestration — long-running workflows that survive restarts, support suspension at arbitrary points, and can be monitored and manually intervened. Building this from scratch is prohibitive. An embedded engine is preferred over an external service to keep deployment simple.

## Decision

Use **Elsa Workflows 3.6.0** embedded directly in the application process.

**NuGet packages (in `Application`):**

| Package | Version | Purpose |
|---------|---------|---------|
| `Elsa` | 3.6.0 | Meta-package: Core, Management, Runtime |
| `Elsa.Workflows.Core` | 3.6.0 | Activity model, workflow engine |
| `Elsa.Workflows.Management` | 3.6.0 | Workflow definition & instance management |
| `Elsa.Workflows.Runtime` | 3.6.0 | Execution runtime, bookmarks, timers |

**Workflow authoring: code-first only.**  
All workflow definitions are authored in C# using `WorkflowBase`. No YAML or JSON definitions in v1.

```csharp
public class OrderFulfillmentWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder
            .WithName("OrderFulfillment")
            .WithVersion(1)
            .Root(new Sequence
            {
                Activities =
                [
                    new ValidateOrderActivity(),
                    new ReserveInventoryActivity(),
                    new SendConfirmationEmailActivity(),
                ]
            });
    }
}
```

Elsa registration lives in `Api/Program.cs` (composition root). Workflow definitions and custom activities are discovered from the `Application` assembly:

```csharp
services.AddElsa(elsa =>
{
    elsa.AddWorkflowsFrom<Application.AssemblyMarker>();
    elsa.AddActivitiesFrom<Application.AssemblyMarker>();
    // MongoDB stores wired separately — see ADR-003
});
```

**Dispatcher rule:** all workflow triggering and resumption must go through `IWorkflowDispatcher`. Never reference `DefaultWorkflowDispatcher` directly — see [ADR-001](001-ddd-layer-structure.md).

## Consequences

**Positive:**
- Workflow definitions are compiled C# — version-controlled, refactor-safe, and fully unit-testable.
- Elsa's bookmark mechanism enables durable suspension without custom infrastructure — see [ADR-004](004-resilience-two-level-strategy.md).
- Embedded deployment — no separate workflow service to manage.
- Code-first approach integrates naturally with the DDD layer rules.

**Negative:**
- No visual designer for workflow definitions in v1 (Studio is added separately — see [ADR-007](007-elsa-studio-embedded.md)).
- Elsa is a significant dependency; upgrading major versions requires testing workflow behaviour thoroughly.
- `IWorkflowDispatcher` is in-process by default — multi-instance deployment requires a dispatcher swap (deferred, see [ADR-001](001-ddd-layer-structure.md)).
