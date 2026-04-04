# ADR-010: Separation of Workflow Logic from Domain Logic

**Status:** Accepted  
**Date:** 2026-04-03

## Context

Elsa activities live in the `Application` project alongside domain service calls. Without a clear boundary, business rules risk leaking into activity code — making them untestable without Elsa, invisible to the domain model, and duplicated across workflows. Equally, workflow orchestration concerns (sequencing, retry, suspension) must not pollute domain objects.

## Decision

Apply a hard separation between what each layer owns:

| Concern | Owner | Lives in |
|---------|-------|----------|
| Business rules, invariants, calculations, state transitions | Domain | `src/Domain` — aggregate methods, domain services, value objects |
| Orchestration: sequencing, branching on workflow state, suspension, retry | Workflow | `src/Application` — `WorkflowBase` definitions, `Activity` subclasses |
| Calling domain services and infrastructure interfaces | Activity (thin) | `src/Application` — activity `ExecuteAsync` |

### The boundary rule

> **An activity must not contain business logic.**
> If code inside `ExecuteAsync` makes a decision based on a business condition, that decision belongs in the domain.

**Wrong — business rule inside an activity:**

```csharp
protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
{
    var order = context.GetVariable<Order>("Order");

    // ❌ Business rule leaked into the activity
    if (order.TotalAmount > 1000 && order.Customer.CreditScore < 600)
        throw new InvalidOperationException("Order rejected: insufficient credit.");

    await _orderRepository.SaveAsync(order);
}
```

**Correct — activity delegates to the domain:**

```csharp
protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
{
    var order = context.GetVariable<Order>("Order");

    // ✅ Domain owns the rule — activity just calls it
    order.Approve();   // throws DomainException if invariants are violated

    await _orderRepository.SaveAsync(order, context.CancellationToken);
}
```

```csharp
// src/Domain/Aggregates/Order.cs
public void Approve()
{
    if (TotalAmount > 1000 && Customer.CreditScore < 600)
        throw new OrderRejectedDomainException("Insufficient credit.");

    Status = OrderStatus.Approved;
    Raise(new OrderApprovedEvent(Id));
}
```

### What activities own

- Calling aggregate methods or domain services.
- Calling injected infrastructure interfaces (`IOrderRepository`, `IPaymentGateway`, `IOutboxWriter`).
- Reading and writing workflow variables (`context.GetVariable`, `context.SetVariable`).
- Creating bookmarks and scheduling delays.
- Setting activity outputs (`context.SetOutput`).
- Catching infrastructure exceptions and triggering durable retry — see [ADR-004](004-resilience-two-level-strategy.md).

### What workflow definitions own

- Step sequencing (`Sequence`, `Parallel`, `Flowchart`).
- Branching on **workflow state** (e.g. "did the previous activity set output X?") — not on business conditions.
- Long-running coordination: waiting for external events via bookmarks, timer-based resumption.
- High-level error paths (e.g. route to a compensation branch on fault).

### What the domain owns

- All business rules and invariants — expressed as aggregate methods or domain services.
- State transition logic — only the aggregate mutates its own state.
- Domain events — raised inside aggregate methods, not from activities.
- Validation of business conditions — never duplicated in activity code.

### Branching in workflows

When a workflow needs to branch on a business outcome, the activity sets a workflow output variable — the workflow definition reads that variable to decide the next step. The business condition itself is evaluated inside the domain method, not in the workflow branch expression.

```csharp
// Activity sets outcome
protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
{
    var result = _creditService.Evaluate(order); // domain service
    context.SetOutput("CreditOutcome", result.Outcome); // Approved | Rejected | ManualReview
}
```

```csharp
// Workflow branches on the output variable — no business logic here
new Switch
{
    Expression = new ActivityOutput<string>(evaluateActivity, "CreditOutcome"),
    Cases =
    [
        new SwitchCase("Approved",      new ProcessPaymentActivity()),
        new SwitchCase("Rejected",      new NotifyRejectionActivity()),
        new SwitchCase("ManualReview",  new EscalateToReviewActivity()),
    ]
}
```

### Testing implication

| What | How | Requires Elsa? |
|------|-----|----------------|
| Domain rule (`Order.Approve`) | Pure unit test | No |
| Activity orchestration | Unit test with mocked interfaces | No (mock `IWorkflowRunner` helpers) |
| Full workflow execution | Integration test via Elsa test helpers | Yes |
| End-to-end scenario | Functional BDD test — see [ADR-008](008-testing-strategy.md) | Yes (full stack) |

Domain rules are always independently testable — no Elsa, no MongoDB, no containers.

## Cross-Aggregate Validation

When a business rule requires loading multiple aggregates of the same type to validate across the group (e.g. "a customer may not have more than 3 pending orders"), the rule is still a domain concern. The pattern below keeps the Domain Service pure while the Application layer handles the I/O.

**Chosen approach: Domain Service receives already-loaded aggregates.**

The Application layer (activity or use case handler) loads the aggregates via the repository, then passes them to a stateless Domain Service that applies the rule in memory. The Domain Service has no repository dependency — it is pure and fully unit-testable with just a list.

```
Activity (Application)
  └── loads aggregates via IOrderRepository
  └── calls IOrderValidationService.Validate(loadedAggregates, newAggregate)
        └── Domain Service applies the rule — no I/O
```

**Domain Service interface and implementation (`src/Domain`):**

```csharp
// src/Domain/Services/IOrderValidationService.cs
public interface IOrderValidationService
{
    void ValidatePendingOrderLimit(IReadOnlyList<Order> existingOrders, Order incomingOrder);
}

// src/Domain/Services/OrderValidationService.cs
public class OrderValidationService : IOrderValidationService
{
    private const int MaxPendingOrders = 3;

    public void ValidatePendingOrderLimit(IReadOnlyList<Order> existingOrders, Order incomingOrder)
    {
        var pendingCount = existingOrders.Count(o => o.Status == OrderStatus.Pending);
        if (pendingCount >= MaxPendingOrders)
            throw new PendingOrderLimitExceededException(incomingOrder.CustomerId, pendingCount);
    }
}
```

**Activity (`src/Application`):**

```csharp
// src/Application/Activities/ValidateOrderActivity.cs
public class ValidateOrderActivity : Activity
{
    private readonly IOrderRepository _repository;
    private readonly IOrderValidationService _validation;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var customerId = context.GetVariable<CustomerId>("CustomerId");
        var newOrder   = context.GetVariable<Order>("NewOrder");

        // Application layer owns the loading
        var existing = await _repository.GetByCustomerAsync(customerId, context.CancellationToken);

        // Domain Service owns the rule — pure, no I/O
        _validation.ValidatePendingOrderLimit(existing, newOrder);
    }
}
```

**Why not put the repository call inside the Domain Service?**

Giving the Domain Service a repository dependency makes it an I/O-bound operation that requires a mock or real database to unit-test. The rule itself (the count threshold, the exception) is pure logic. Separating the two keeps each part independently testable:

| What | Tested how | Requires infrastructure? |
|------|-----------|--------------------------|
| `ValidatePendingOrderLimit` rule | Unit test — pass a plain `List<Order>` | No |
| Loading via `IOrderRepository` | Integration test — Testcontainers MongoDB | Yes |
| Full activity orchestration | Integration / functional test | Yes |

**Responsibility table:**

| Responsibility | Owner |
|---------------|-------|
| What the rule is (threshold, exception) | Domain Service (`src/Domain`) |
| Loading the aggregates needed | Activity or use-case handler (`src/Application`) |
| When the validation runs in the process | Elsa workflow definition (`src/Application`) |

---

## Consequences

**Positive:**
- Domain logic is fully unit-testable without any Elsa infrastructure.
- Activities stay thin — easy to read, easy to test, easy to swap.
- Business rules cannot be bypassed by triggering a different workflow path — they are always enforced at the domain level.
- Workflow definitions become a readable map of process steps, not a tangle of business conditions.

**Negative:**
- Requires discipline — the boundary is enforced by convention and code review, not by the compiler. Consider adding an ArchUnit rule that flags non-trivial branching logic inside `Activity` subclasses.
- Developers new to DDD may default to putting conditions in activities — the examples in this ADR should be the canonical reference in code review.
