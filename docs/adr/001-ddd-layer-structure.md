# ADR-001: DDD Layer Structure

**Status:** Accepted  
**Date:** 2026-04-03

## Context

The solution must separate business rules from infrastructure concerns. Domain logic must be independently testable with no external dependencies. The architecture must also remain forward-compatible with a future move to multi-instance deployment.

## Decision

Adopt a four-project DDD structure:

```
src/
├── Domain/          # Pure business model — zero external NuGet packages
├── Application/     # Use cases, Elsa workflow definitions and activities
├── Infrastructure/  # MongoDB repositories, resilience, outbox
└── Api/             # ASP.NET Core composition root, customer-facing endpoints
```

**Dependency rules (strictly enforced):**

```
Api            → Application, Infrastructure   (composition root only)
Application    → Domain
Infrastructure → Domain
Domain         → (nothing external — BCL only)

Elsa packages  → Application, Infrastructure, Api   NOT Domain
MongoDB driver → Infrastructure, Api                NOT Domain, NOT Application
```

**Additional rules:**

| Rule | Reason |
|------|--------|
| `Application` must not reference `DefaultWorkflowDispatcher` | Dispatcher must be swappable via a single DI registration — see Consequences |
| `Application` must not reference `HttpClient` or any broker SDK | External I/O is Infrastructure's concern |
| Concrete Polly types must not appear in `Application` | Resilience strategy is Infrastructure's concern |

Enforce via a Roslyn analyser or ArchUnit-style tests in `Integration.Tests`.

## Consequences

**Positive:**
- Domain is purely unit-testable — no mocks, no containers needed.
- Elsa and MongoDB can be swapped or upgraded without touching Domain.
- The `IWorkflowDispatcher` constraint means moving from single-instance (`DefaultWorkflowDispatcher`) to multi-instance (Rebus / MassTransit / Hangfire dispatcher) requires only one registration change in `Api/Program.cs`.

**Negative:**
- More projects than a simple layered architecture — onboarding takes slightly longer.
- Strict dependency rules require tooling to enforce; without it, violations creep in silently.
