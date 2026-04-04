# ADR-003: MongoDB as the Single Persistence Store

**Status:** Accepted  
**Date:** 2026-04-03

## Context

The solution needs persistence for two distinct concerns: domain aggregates and Elsa workflow state. Using two different databases adds operational overhead. MongoDB's document model fits naturally with both DDD aggregates and Elsa's workflow state storage.

## Decision

Use **MongoDB** as the single persistence technology for the entire solution — domain data and Elsa workflow state share one database.

**NuGet packages (in `Infrastructure`):**

| Package | Version | Purpose |
|---------|---------|---------|
| `MongoDB.Driver` | latest stable | Domain repository implementations |
| `Elsa.Persistence.MongoDb` | 3.6.0 | Elsa workflow state stores |

**Elsa store configuration — both modules must be explicitly pointed at MongoDB.** Omitting either causes that module to fall back to its default in-memory store.

```csharp
services.AddElsa(elsa =>
{
    elsa.UseWorkflowManagement(mgmt => mgmt.UseMongoDB(mongo =>
    {
        mongo.ConnectionString = config["MongoDB:ConnectionString"]!;
        mongo.DatabaseName     = config["MongoDB:DatabaseName"]!;
    }));

    elsa.UseWorkflowRuntime(rt => rt.UseMongoDB(mongo =>
    {
        mongo.ConnectionString = config["MongoDB:ConnectionString"]!;
        mongo.DatabaseName     = config["MongoDB:DatabaseName"]!;
    }));
});
```

**Collection layout:**

| Collection | Owner | Notes |
|-----------|-------|-------|
| `workflowDefinitions` | Elsa | Managed by `Elsa.Persistence.MongoDb` |
| `workflowInstances` | Elsa | Running / completed workflow state |
| `workflowBookmarks` | Elsa | Suspension points |
| `workflowTriggers` | Elsa | Stored trigger records |
| `workflowExecutionLogRecords` | Elsa | Activity execution history |
| `outboxMessages` | Infrastructure | Outbox for reliable message publishing — see [ADR-005](005-outbox-pattern.md) |
| `{aggregateName}` | Infrastructure | One collection per domain aggregate root |

**MongoDB conventions (domain collections):**
- `camelCase` element names via BSON conventions.
- Aggregate root `Id` mapped to `_id` as `string` GUID.
- Index definitions declared alongside collection registrations, not scattered.

**Configuration skeleton:**

```jsonc
{
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "elsa_ddd_db"
  }
}
```

## Consequences

**Positive:**
- Single database — one connection string, one backup strategy, one operational concern.
- Document model maps naturally to DDD aggregates with no ORM impedance mismatch.
- Elsa's MongoDB persistence (`Elsa.Persistence.MongoDb`) is a first-class supported package.
- The outbox collection lives in the same database — atomic writes between domain state and outbox are possible within a MongoDB multi-document transaction.

**Negative:**
- Elsa workflow state and domain data share a database — schema changes to Elsa's collections (across Elsa upgrades) can affect the same DB instance.
- No relational queries; complex reporting or cross-aggregate queries require aggregation pipelines or a separate read model.
- Multi-instance safety for the outbox dispatcher requires the `lockedUntil` optimistic lock field — see [ADR-005](005-outbox-pattern.md).
