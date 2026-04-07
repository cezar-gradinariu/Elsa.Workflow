# Elsa Workflow DDD Solution

A .NET 10 solution built on Domain-Driven Design principles, using Elsa Workflows 3 for process orchestration and MongoDB for all persistence.

**Stack:** C# 13 · .NET 10 · [Elsa Workflows 3.6.0](https://docs.elsaworkflows.io/) · MongoDB

---

## Solution Structure

```text
Solution.sln
├── src/
│   ├── Domain/            # Pure business model — zero external NuGet packages
│   ├── Application/       # Use cases, Elsa workflow definitions and activities
│   ├── Infrastructure/    # MongoDB repositories, resilience pipelines, outbox
│   └── Api/               # ASP.NET Core Web API with Elsa endpoints
└── tests/
    ├── Domain.Tests/      # Unit tests — pure in-memory
    ├── Application.Tests/ # Unit tests — mocked infrastructure
    ├── Integration.Tests/ # Integration tests — Testcontainers MongoDB
    └── Functional.Tests/  # BDD black-box tests — Reqnroll + Testcontainers
```

---

## Documentation

| Document | Description |
| -------- | ----------- |
| [PRD.md](PRD.md) | Index of all documentation and ADRs |
| [docs/prd.md](docs/prd.md) | Product requirements — goals, functional requirements, user stories |
| [docs/glossary.md](docs/glossary.md) | Term definitions |

---

## Architecture Decision Records

| ADR | Title |
| --- | ----- |
| [ADR-001](docs/adr/001-ddd-layer-structure.md) | DDD Layer Structure |
| [ADR-002](docs/adr/002-elsa-workflow-engine.md) | Elsa Workflows as the Embedded Orchestration Engine |
| [ADR-003](docs/adr/003-mongodb-persistence.md) | MongoDB as the Single Persistence Store |
| [ADR-004](docs/adr/004-resilience-two-level-strategy.md) | Two-Level Resilience Strategy for External Calls |
| [ADR-005](docs/adr/005-outbox-pattern.md) | Outbox Pattern for Reliable Message Publishing |
| [ADR-006](docs/adr/006-circuit-breaker.md) | Circuit Breaker for External API Calls |
| [ADR-007](docs/adr/007-elsa-studio-embedded.md) | Elsa Studio Embedded in the Api Project *(superseded by ADR-014)* |
| [ADR-008](docs/adr/008-testing-strategy.md) | Testing Strategy |
| [ADR-009](docs/adr/009-testcontainers-policy.md) | Testcontainers Module Policy |
| [ADR-010](docs/adr/010-workflow-vs-domain-separation.md) | Separation of Workflow Logic from Domain Logic |
| [ADR-011](docs/adr/011-problem-details-error-responses.md) | Problem Details Error Responses |
| [ADR-012](docs/adr/012-prohibited-libraries.md) | Prohibited Libraries |
| [ADR-013](docs/adr/013-ofa-integration-workflow-step.md) | OFA Integration Workflow Step |
| [ADR-014](docs/adr/014-elsa-studio-docker.md) | Elsa Studio via Official Docker Image |

---

## Key Design Principles

- **Domain is pure** — `src/Domain` has zero NuGet dependencies. Business rules are independent of any framework.
- **Elsa owns orchestration** — all long-running process logic lives in code-first workflow definitions inside `src/Application`.
- **Infrastructure is a detail** — MongoDB, Polly resilience pipelines, and the outbox dispatcher are wired in `src/Infrastructure` and invisible to the domain and application layers.
- **Reliable by design** — external calls use a two-level resilience strategy: Polly (fast, in-process) + Elsa durable retry (suspend to MongoDB, resume after delay).
- **Dispatcher-agnostic** — all workflow triggering goes through `IWorkflowDispatcher`. Swapping to a distributed dispatcher for multi-instance deployment requires one registration change.

---

## Development Setup

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Architecture

```text
┌──────────────────┐   HTTP /elsa/api   ┌─────────────────┐
│  Elsa Studio     │ ◄────────────────► │   Domain API    │
│  (Docker)        │                    │   (dotnet run)  │
│  localhost:6002  │                    │  localhost:5158 │
└──────────────────┘                    └────────┬────────┘
                                                 │
                                        ┌────────▼────────┐
                                        │    MongoDB      │
                                        │    (Docker)     │
                                        │  localhost:27017│
                                        └─────────────────┘
```

The Elsa Studio image is a pure UI — it has no database of its own. It connects to the Domain API over HTTP, which owns all MongoDB persistence.

### 1. Start infrastructure (MongoDB + Elsa Studio)

```bash
docker-compose up -d
```

This starts:
- **MongoDB** on `localhost:27017` (credentials: `admin` / `admin`)
- **Elsa Studio** on `http://localhost:6002` (connects to the API via `host.docker.internal:5158`)

### 2. Start the Domain API

```bash
dotnet run --project src/Api
```

The API starts on `http://localhost:5158`.

### 3. Open the tools

| Tool | URL | Credentials |
| ---- | --- | ----------- |
| Elsa Studio | http://localhost:6002 | `admin` / `password` |
| Swagger UI | http://localhost:5158/swagger | — |
| Elsa REST API | http://localhost:5158/elsa/api | — |

> **CORS note:** CORS is configured with `AllowAnyOrigin` in development so the Studio container at port 6002 can reach the API without issue.

### Stopping services

```bash
# Stop containers (preserves MongoDB data volume)
docker-compose down

# Stop containers AND wipe all data
docker-compose down -v
```

---

## API Endpoints

### Domain API

| Method | Endpoint | Description |
| ------ | -------- | ----------- |
| `POST` | `/api/fulfilments` | Create a fulfilment order |
| `GET` | `/api/fulfilments/{id}` | Get a fulfilment order |
| `DELETE` | `/api/fulfilments/{id}` | Delete a fulfilment order |

### Elsa Workflow API (managed by Elsa)

| Method | Endpoint | Description |
| ------ | -------- | ----------- |
| `GET` | `/elsa/api/workflow-definitions` | List workflow definitions |
| `GET` | `/elsa/api/workflow-instances` | List workflow instances |
| `GET` | `/elsa/api/activity-types` | List available activity types |

Full API reference: [docs.elsaworkflows.io](https://docs.elsaworkflows.io/docs/guides/workflow-management-api)

---

## External References

| Resource | URL |
| -------- | --- |
| Elsa Workflows documentation | [docs.elsaworkflows.io](https://docs.elsaworkflows.io/) |
| Elsa GitHub repository | [github.com/elsa-workflows/elsa-core](https://github.com/elsa-workflows/elsa-core) |
| Testcontainers for .NET | [dotnet.testcontainers.org](https://dotnet.testcontainers.org/) |
| Reqnroll (BDD framework) | [reqnroll.net](https://reqnroll.net/) |
| Polly resilience library | [github.com/App-vNext/Polly](https://github.com/App-vNext/Polly) |
| MongoDB .NET Driver | [mongodb.com/docs/drivers/csharp](https://www.mongodb.com/docs/drivers/csharp/) |
