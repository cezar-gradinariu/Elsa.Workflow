# Glossary

| Term | Meaning |
|------|---------|
| Aggregate Root | Top-level domain entity that enforces invariants for a cluster of objects |
| Activity | Elsa unit of work — equivalent to a step in a workflow |
| Alterations API | Elsa mechanism to modify a running or faulted workflow instance programmatically (e.g. re-schedule an activity) |
| At-Least-Once Delivery | Guarantee that a message is delivered at minimum once; consumers must handle duplicates idempotently |
| Bookmark | Elsa mechanism for suspending a workflow at a point and resuming later |
| Circuit Breaker | Polly policy that stops calling a failing dependency for a cooldown period after a threshold of failures |
| Durable Retry | Elsa-level retry where the workflow suspends to MongoDB and resumes after a delay — survives process restarts |
| Incident | Elsa's record of an activity failure; visible in Studio and queryable via API |
| IIncidentStrategy | Elsa extension point that controls what happens when an activity faults — default is to record and fault; can be overridden to suspend instead |
| Outbox Pattern | Persist a message to the local DB in the same transaction as domain state; a background worker dispatches it to the broker asynchronously |
| POCO | Plain Old CLR Object — no framework attributes or base classes |
| Retry Pipeline | Polly policy that re-attempts a failed call with exponential back-off and jitter |
| Workflow Definition | Blueprint of a workflow (code-first `WorkflowBase` in this solution) |
| Workflow Instance | Runtime execution of a workflow definition |
