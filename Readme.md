# Elsa Workflow DDD Solution

A .NET 10 solution built on Domain-Driven Design principles, using Elsa Workflows 3 for process orchestration and MongoDB for all persistence.

**Stack:** C# 13 · .NET 10 · [Elsa Workflows 3.6.0](https://docs.elsaworkflows.io/) · MongoDB

---

## Solution Structure

```
Solution.sln
├── src/
│   ├── Domain/            # Pure business model — zero external NuGet packages
│   ├── Application/       # Use cases, Elsa workflow definitions and activities
│   ├── Infrastructure/    # MongoDB repositories, resilience pipelines, outbox
│   └── Api/               # ASP.NET Core Web API + embedded Elsa Studio
└── tests/
    ├── Domain.Tests/      # Unit tests — pure in-memory
    ├── Application.Tests/ # Unit tests — mocked infrastructure
    ├── Integration.Tests/ # Integration tests — Testcontainers MongoDB
    └── Functional.Tests/  # BDD black-box tests — Reqnroll + Testcontainers
```

---

## Documentation

| Document | Description |
|----------|-------------|
| [PRD.md](PRD.md) | Index of all documentation and ADRs |
| [docs/prd.md](docs/prd.md) | Product requirements — goals, functional requirements, user stories |
| [docs/glossary.md](docs/glossary.md) | Term definitions |

---

## Architecture Decision Records

| ADR | Title |
|-----|-------|
| [ADR-001](docs/adr/001-ddd-layer-structure.md) | DDD Layer Structure |
| [ADR-002](docs/adr/002-elsa-workflow-engine.md) | Elsa Workflows as the Embedded Orchestration Engine |
| [ADR-003](docs/adr/003-mongodb-persistence.md) | MongoDB as the Single Persistence Store |
| [ADR-004](docs/adr/004-resilience-two-level-strategy.md) | Two-Level Resilience Strategy for External Calls |
| [ADR-005](docs/adr/005-outbox-pattern.md) | Outbox Pattern for Reliable Message Publishing |
| [ADR-006](docs/adr/006-circuit-breaker.md) | Circuit Breaker for External API Calls |
| [ADR-007](docs/adr/007-elsa-studio-embedded.md) | Elsa Studio Embedded in the Api Project |
| [ADR-008](docs/adr/008-testing-strategy.md) | Testing Strategy |
| [ADR-009](docs/adr/009-testcontainers-policy.md) | Testcontainers Module Policy |
| [ADR-010](docs/adr/010-workflow-vs-domain-separation.md) | Separation of Workflow Logic from Domain Logic |

---

## Key Design Principles

- **Domain is pure** — `src/Domain` has zero NuGet dependencies. Business rules are independent of any framework.
- **Elsa owns orchestration** — all long-running process logic lives in code-first workflow definitions inside `src/Application`.
- **Infrastructure is a detail** — MongoDB, Polly resilience pipelines, and the outbox dispatcher are wired in `src/Infrastructure` and invisible to the domain and application layers.
- **Reliable by design** — external calls use a two-level resilience strategy: Polly (fast, in-process) + Elsa durable retry (suspend to MongoDB, resume after delay).
- **Dispatcher-agnostic** — all workflow triggering goes through `IWorkflowDispatcher`. Swapping to a distributed dispatcher for multi-instance deployment requires one registration change.

---

## External References

> **Elsa reference priority:** Always consult the [Elsa GitBook](https://github.com/elsa-workflows/elsa-gitbook) **first** for authoritative guides, recipes, and API documentation. Fall back to source code and other resources only if the GitBook does not cover the topic.

| Resource | URL |
|----------|-----|
| **Elsa GitBook (primary reference)** | [github.com/elsa-workflows/elsa-gitbook](https://github.com/elsa-workflows/elsa-gitbook) |
| Elsa Workflows documentation | [docs.elsaworkflows.io](https://docs.elsaworkflows.io/) |
| Elsa GitHub repository | [github.com/elsa-workflows/elsa-core](https://github.com/elsa-workflows/elsa-core) |
| Testcontainers for .NET | [dotnet.testcontainers.org](https://dotnet.testcontainers.org/) |
| Reqnroll (BDD framework) | [reqnroll.net](https://reqnroll.net/) |
| Polly resilience library | [github.com/App-vNext/Polly](https://github.com/App-vNext/Polly) |
| MongoDB .NET Driver | [mongodb.com/docs/drivers/csharp](https://www.mongodb.com/docs/drivers/csharp/) |
| .NET Central Package Management | [learn.microsoft.com/nuget/consume-packages/central-package-management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management) |
