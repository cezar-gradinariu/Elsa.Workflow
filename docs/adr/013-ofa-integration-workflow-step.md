# ADR-013: OFA Integration — Workflow Step and Allocation Schema

**Status:** Accepted  
**Date:** 2026-04-05

## Context

Once a `FulfilmentOrder` aggregate is created and its workflow is started, the first substantive step is to call the **Order Fulfilment Allocator (OFA)** — an external HTTP service. OFA receives the order's picking context and returns a sub-store allocation plan: which sub-stores will satisfy which order lines, and in what quantities. Allocations may be full (one sub-store covers all quantity for a line) or partial (quantity split across multiple sub-stores).

The call must be resilient — OFA may be slow or temporarily unavailable. The two-level resilience strategy applies (ADR-004): Polly retry + circuit breaker at the Infrastructure level (ADR-006), and Elsa durable suspend/resume at the Activity level.

## Decision

### Workflow step placement

`CallOfaActivity` is the first substantive activity in `FulfilmentOrderWorkflow`, immediately following workflow start.

```
FulfilmentOrderWorkflow
  └── Sequence
        ├── CallOfaActivity          ← OFA HTTP call (resilient)
        └── [future activities]
```

The activity carries **only** `FulfilmentOrderId` as a workflow variable. All data needed for the OFA request is loaded from the `IFulfilmentOrderRepository` inside the activity — the aggregate is never stored in workflow execution state. See ADR-010 for the boundary rule.

### OFA request payload

```csharp
public record OfaAllocationRequest(
    string  OrderId,
    string  StoreId,
    IReadOnlyList<OfaOrderLineRequest> OrderLines
);

public record OfaOrderLineRequest(
    int     OrderLineNo,
    string  ArticleId,
    decimal ExpectedQuantity,
    string  UnitOfMeasure        // "Ea" | "Kg"
);
```

### OFA response schema

**Interpretation A confirmed** — each order line is fully accounted for across one or more sub-stores. The sum of `AllocatedQuantity` across sub-stores for any given line always equals the `ExpectedQuantity`.

**Interpretation A — Fully allocated per line (total allocated = expected)**

Each order line is fully accounted for across one or more sub-stores. The sum of `AllocatedQuantity` across sub-stores for any given line always equals the `ExpectedQuantity`.

```json
{
  "fulfilmentOrderId": "...",
  "subStoreAllocations": [
    {
      "subStoreId": "STORE-A",
      "lines": [
        { "orderLineNo": 1, "articleId": "SKU-001", "allocatedQuantity": 3.0, "unitOfMeasure": "Ea" },
        { "orderLineNo": 2, "articleId": "SKU-002", "allocatedQuantity": 10.0, "unitOfMeasure": "Kg" }
      ]
    },
    {
      "subStoreId": "STORE-B",
      "lines": [
        { "orderLineNo": 1, "articleId": "SKU-001", "allocatedQuantity": 2.0, "unitOfMeasure": "Ea" }
      ]
    }
  ]
}
```

Aggregate validation rule: `SUM(allocatedQuantity) == ExpectedQuantity` for every line.

---

**Interpretation B — Partial allocation possible (total allocated ≤ expected)**

OFA may not be able to fulfil a line in full (stock shortage). A line's allocation can be less than its expected quantity. The aggregate records both expected and allocated totals per line.

```json
{
  "fulfilmentOrderId": "...",
  "subStoreAllocations": [
    {
      "subStoreId": "STORE-A",
      "lines": [
        { "orderLineNo": 1, "articleId": "SKU-001", "allocatedQuantity": 3.0, "unitOfMeasure": "Ea" },
        { "orderLineNo": 2, "articleId": "SKU-002", "allocatedQuantity": 7.5, "unitOfMeasure": "Kg" }
      ]
    }
  ]
}
```

Aggregate validation rule: `SUM(allocatedQuantity) <= ExpectedQuantity` for every line.

---

### Proposed aggregate OrderLine update shape (after allocation)

```csharp
public class OrderLine
{
    public int     OrderLineNo                   { get; private set; }
    public string  ArticleId                     { get; private set; }
    public decimal ExpectedQuantity              { get; private set; }
    public UnitOfMeasure UnitOfMeasure           { get; private set; }
    public string? CustomerSupplyInstructions    { get; private set; }

    // Set after OFA call via FulfilmentOrder.ApplyAllocation(...)
    public AllocationStatus AllocationStatus     { get; private set; }   // Pending | FullyAllocated | PartiallyAllocated | Unallocated
    public IReadOnlyList<SubStoreAllocation> Allocations { get; private set; } = [];
}

public record SubStoreAllocation(
    string  SubStoreId,
    decimal AllocatedQuantity
);

public enum AllocationStatus { Pending, FullyAllocated, PartiallyAllocated, Unallocated }
```

### `FulfilmentOrder.ApplyAllocation(...)` — validation rules (aggregate method)

1. Every `OrderLineNo` from the original order lines **must be present** in the OFA response.
2. For each line: `SUM(allocatedQuantity across sub-stores)` must match the expected quantity (Interpretation A) or must not exceed it (Interpretation B). **TBC.**
3. No negative allocated quantities.
4. After passing validation, update each `OrderLine.Allocations` and set `AllocationStatus`.
5. Update aggregate-level `Status` to `Allocated`.

### Resilience

`CallOfaActivity` wraps its HTTP call through the named `HttpClient("OFA")` registered in Infrastructure with `.AddStandardResilienceHandler()` (ADR-006, Option A). Polly exhaustion triggers durable Level-2 Elsa suspend/resume per ADR-004.

### After successful allocation

1. `CallOfaActivity` calls `FulfilmentOrder.ApplyAllocation(...)`.
2. Repository saves the updated aggregate (optimistic concurrency check on `Version`).
3. Workflow suspends (bookmark) — awaiting next trigger.

The workflow does **not** go to `Completed` here; the bookmark enables future resumption for downstream process steps.

## Open Questions

| # | Question | Owner | Status |
|---|----------|-------|--------|
| OQ-1 | Which interpretation of allocation (A = fully allocated, B = partial allowed)? | Business | **Resolved — Interpretation A** |
| OQ-2 | Does OFA ever return lines not present in the request? Should extras be ignored or rejected? | Business | Awaiting answer |
| OQ-3 | What happens if OFA permanently fails (exhausts durable retries)? Manual intervention via Elsa Alterations API, or auto-cancel? | Product | Awaiting answer |

## Consequences

**Positive:**
- Aggregate carries the full allocation result — single source of truth for fulfilment state.
- Activity is thin; all validation is in the domain — consistent with ADR-010.
- Durable retry means OFA downtime does not lose the workflow — it simply suspends.

**Negative:**
- Workflow suspension means the caller gets a 200 on `POST /api/fulfilments` without knowing whether allocation succeeded. The GET endpoint reflects current state (including post-allocation `OrderLine.Allocations`).
- The OFA schema uncertainty (OQ-1) blocks finalisation of `ApplyAllocation` validation logic and the full activity implementation.
