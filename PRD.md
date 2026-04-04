# Elsa Workflow DDD Solution

**Stack:** C# 13 / .NET 10 · Elsa Workflows 3.6.0 · MongoDB

---

## Documentation

| Document | Description |
|----------|-------------|
| [docs/prd.md](docs/prd.md) | Product requirements — goals, functional requirements, user stories |
| [docs/glossary.md](docs/glossary.md) | Term definitions |

## Architecture Decision Records

| ADR | Title | Status |
|-----|-------|--------|
| [ADR-001](docs/adr/001-ddd-layer-structure.md) | DDD Layer Structure | Accepted |
| [ADR-002](docs/adr/002-elsa-workflow-engine.md) | Elsa Workflows as the Embedded Orchestration Engine | Accepted |
| [ADR-003](docs/adr/003-mongodb-persistence.md) | MongoDB as the Single Persistence Store | Accepted |
| [ADR-004](docs/adr/004-resilience-two-level-strategy.md) | Two-Level Resilience Strategy for External Calls | Accepted |
| [ADR-005](docs/adr/005-outbox-pattern.md) | Outbox Pattern for Reliable Message Publishing | Accepted |
| [ADR-006](docs/adr/006-circuit-breaker.md) | Circuit Breaker for External API Calls | Accepted |
| [ADR-007](docs/adr/007-elsa-studio-embedded.md) | Elsa Studio Embedded in the Api Project | Accepted |
| [ADR-008](docs/adr/008-testing-strategy.md) | Testing Strategy | Accepted |
| [ADR-009](docs/adr/009-testcontainers-policy.md) | Testcontainers Module Policy | Accepted |
| [ADR-010](docs/adr/010-workflow-vs-domain-separation.md) | Separation of Workflow Logic from Domain Logic | Accepted |
