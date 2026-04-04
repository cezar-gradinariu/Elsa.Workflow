# ADR-005: Outbox Pattern for Reliable Message Publishing

**Status:** Accepted  
**Date:** 2026-04-03

## Context

Workflow activities may need to publish messages to external brokers (RabbitMQ, Azure Service Bus, Kafka, etc.). If the broker is unavailable when the activity runs, the message is lost. A distributed transaction spanning MongoDB and a broker is not practical. Elsa has no built-in outbox for external message publishing — `WorkflowInbox` is an internal mechanism for routing stimulus between workflows only.

## Decision

Implement a **MongoDB-backed outbox** in the `Infrastructure` project. Publishing is split into two phases:

**Phase 1 — Write intent (inside the activity, atomic with domain state):**

The activity writes an `OutboxMessage` document to MongoDB. No broker call happens here. The write is in the same MongoDB session as any domain state changes, giving atomic intent recording without a distributed transaction.

```csharp
public class NotifyOrderConfirmedActivity : Activity
{
    private readonly IOutboxWriter _outbox;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var message = new OrderConfirmedMessage(/* ... */);
        await _outbox.WriteAsync(message, context.CancellationToken);
    }
}
```

**Phase 2 — Dispatch (background, independent of the workflow):**

`OutboxDispatcherService` (`BackgroundService` in `Infrastructure`) polls the `outboxMessages` collection on a short interval (default: 5 s), publishes each pending message to the broker using the Level 1 Polly retry pipeline, then marks it dispatched.

```csharp
public class OutboxDispatcherService : BackgroundService
{
    // For each undispatched, unlocked message:
    //   1. Claim it (set lockedUntil = now + lockDuration)
    //   2. Publish to broker via resilience pipeline
    //   3. Mark dispatchedAt = now (or delete)
    //   4. On failure: release lock — next poll will retry
}
```

**`OutboxMessage` document shape:**

| Field | Type | Notes |
|-------|------|-------|
| `_id` | string (GUID) | Unique message id |
| `messageType` | string | Fully-qualified CLR type name |
| `payload` | string (JSON) | Serialised message body |
| `createdAt` | DateTime | Written timestamp |
| `dispatchedAt` | DateTime? | Null until successfully dispatched |
| `lockedUntil` | DateTime? | Optimistic lock — prevents duplicate dispatch under multiple instances |

**Interfaces (in `Application`):**

```
IOutboxWriter   — WriteAsync(message, ct): called from activities
IOutboxReader   — ReadPendingAsync(ct): called by OutboxDispatcherService
```

Implementation lives in `Infrastructure`; `Application` depends only on `IOutboxWriter`.

## Consequences

**Positive:**
- Messages survive broker downtime — the intent is persisted to MongoDB before the broker is ever contacted.
- No second data store — the outbox lives in the same MongoDB database as domain data and Elsa state.
- The `lockedUntil` field makes the dispatcher safe under multiple instances without external coordination.
- At-least-once delivery: if the dispatcher crashes after publishing but before marking dispatched, the message is re-published on the next poll. Consumers **must be idempotent**.

**Negative:**
- Polling introduces latency (up to the poll interval, default 5 s). Reduce interval for near-real-time publishing requirements.
- Consumers must handle duplicate messages — this is a design constraint on downstream systems.
- The outbox grows without a cleanup strategy — a TTL index on `dispatchedAt` is recommended to auto-expire old records.
