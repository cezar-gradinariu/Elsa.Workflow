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

`elsaworkflows/elsa-studio-v3` is a pure static-file server — it serves the Blazor WASM bundle only. Every API call is made by the **browser**, so the API URL must be reachable from `localhost`, not from inside Docker.

### 1. Start infrastructure (MongoDB + Elsa Studio)

```bash
docker-compose up -d
```

Starts:
- **MongoDB** on `localhost:27017` (credentials: `admin` / `admin`)
- **Elsa Studio** on `http://localhost:6002`

### 2. Start the Domain API

```bash
dotnet run --project src/Api
```

API starts on `http://localhost:5158`.

### 3. Open the tools

| Tool | URL | Credentials |
| ---- | --- | ----------- |
| Elsa Studio | http://localhost:6002 | `admin` / `password` |
| Swagger UI | http://localhost:5158/swagger | — |
| Elsa REST API | http://localhost:5158/elsa/api | — |

### Stopping services

```bash
# Stop containers (preserves MongoDB data volume)
docker-compose down

# Stop containers AND wipe all data
docker-compose down -v
```

---

## Elsa Configuration Notes

These are non-obvious requirements that must be in place for the Studio and API to work correctly.

### `elsa.UseDefaultAuthentication()` is mandatory

`UseIdentity` alone does **not** activate JWT authentication. `DefaultAuthenticationFeature` — which registers the `"Jwt-or-ApiKey"` policy scheme used for all protected endpoints — is a separate opt-in:

```csharp
builder.Services.AddElsa(elsa =>
{
    elsa.UseIdentity(identity => {
        identity.TokenOptions = options => options.SigningKey = "...";
        identity.UseAdminUserProvider();
    });

    elsa.UseDefaultAuthentication(); // required — separate call on IModule
});
```

Without this, every authenticated request returns **500** ("No DefaultChallengeScheme found").

### MongoDB connection string must contain the database name

Elsa's MongoDB feature reads the database name from the `MongoUrl` path segment. A separate `DatabaseName` config key is **not** used:

```json
"MongoDB": {
  "ConnectionString": "mongodb://admin:admin@localhost:27017/elsa_ddd_db?authSource=admin"
}
```

### Do not pre-register `IMongoDatabase` as Scoped

Registering `IMongoDatabase` with `AddScoped` before `AddElsa` prevents Elsa's `UseMongoDb` (which uses `TryAddSingleton`) from registering its own. Elsa's singleton workflow stores then capture a scoped dependency, causing a scope validation error at startup in Development mode. Let Elsa own both `IMongoClient` and `IMongoDatabase`.

### Do not call `app.UseAuthentication()` / `app.UseAuthorization()` manually

`UseWorkflowsApi()` calls these internally as part of its FastEndpoints pipeline. Adding them again in the outer pipeline conflicts with Elsa's identity scheme setup.

### CORS — `app.UseCors()` must be first

CORS middleware must be registered before `UseExceptionHandler` so that CORS headers are present on **all** responses, including error responses. The Studio makes cross-origin requests (`localhost:6002` → `localhost:5158`) and will show "An unhandled error has occurred" if CORS headers are missing from any response it receives.

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
