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
|----------|-------------|
| [PRD.md](PRD.md) | Index of all documentation and ADRs |
| [docs/prd.md](docs/prd.md) | Product requirements — goals, functional requirements, user stories |
| [docs/glossary.md](docs/glossary.md) | Term definitions |
| [docs/elsa-studio-mongodb-limitation.md](docs/elsa-studio-mongodb-limitation.md) | ❌ **KNOWN ISSUE** - Elsa Studio MongoDB integration limitation |

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
| [ADR-011](docs/elsa-studio-mongodb-limitation.md) | ❌ Elsa Studio MongoDB Integration Limitation |
| [ADR-014](docs/adr/014-elsa-studio-docker.md) | Elsa Studio via Official Docker Image |

---

## Key Design Principles

- **Domain is pure** — `src/Domain` has zero NuGet dependencies. Business rules are independent of any framework.
- **Elsa owns orchestration** — all long-running process logic lives in code-first workflow definitions inside `src/Application`.
- **Infrastructure is a detail** — MongoDB, Polly resilience pipelines, and the outbox dispatcher are wired in `src/Infrastructure` and invisible to the domain and application layers.
- **Reliable by design** — external calls use a two-level resilience strategy: Polly (fast, in-process) + Elsa durable retry (suspend to MongoDB, resume after delay).
- **Dispatcher-agnostic** — all workflow triggering goes through `IWorkflowDispatcher`. Swapping to a distributed dispatcher for multi-instance deployment requires one registration change.

---

## 🐳 Docker Development Setup

### Quick Start

This guide explains how to set up Elsa Workflows locally using Docker containers for development.

### Prerequisites

- Docker Desktop installed and running
- .NET 10 SDK (for custom domain API)
- PowerShell or Command Prompt

### Architecture Overview

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│  Elsa Studio    │    │   Domain API     │    │   MongoDB       │
│  + Server       │    │   (Your App)     │    │   Database      │
│  :14740         │    │   :5158          │    │   :27017        │
└─────────────────┘    └──────────────────┘    └─────────────────┘
```

### 1. Start MongoDB Database

```powershell
docker run -d --name my-mongo `
  -e MONGO_INITDB_ROOT_USERNAME=admin `
  -e MONGO_INITDB_ROOT_PASSWORD=admin `
  -p 27017:27017 `
  mongo:latest
```

### 2. Start Elsa Studio + Server

```powershell
docker run -d --name elsa-studio-server `
  -e ASPNETCORE_ENVIRONMENT=Development `
  -e ASPNETCORE_URLS=http://+:80 `
  -e ConnectionStrings__Default="mongodb://admin:admin@host.docker.internal:27017/elsa_ddd_db?authSource=admin" `
  -p 14740:80 `
  elsaworkflows/elsa-server-and-studio-v3
```

### 3. Start Domain API (Optional)

```powershell
cd src/Api
dotnet run
```

### Default Credentials

| Service | Username | Password | URL |
|---------|----------|----------|-----|
| MongoDB | `admin` | `admin` | localhost:27017 |
| Elsa Studio | `admin` | `password` | http://localhost:14740 |
| Domain API | - | - | http://localhost:5158/api/WorkflowTest/test |

### Automated Setup Script

Create `start-elsa.ps1` in the solution root:

```powershell
# Elsa Workflow Docker Setup Script
Write-Host "🚀 Starting Elsa Workflow Environment..." -ForegroundColor Green

# Stop existing containers (if any)
docker stop my-mongo elsa-studio-server 2>$null
docker rm my-mongo elsa-studio-server 2>$null

# Start MongoDB
Write-Host "📦 Starting MongoDB..." -ForegroundColor Blue
docker run -d --name my-mongo `
  -e MONGO_INITDB_ROOT_USERNAME=admin `
  -e MONGO_INITDB_ROOT_PASSWORD=admin `
  -p 27017:27017 `
  mongo:latest

# Wait for MongoDB to be ready
Start-Sleep -Seconds 10

# Start Elsa Studio + Server
Write-Host "🎨 Starting Elsa Studio + Server..." -ForegroundColor Blue
docker run -d --name elsa-studio-server `
  -e ASPNETCORE_ENVIRONMENT=Development `
  -e ASPNETCORE_URLS=http://+:80 `
  -e ConnectionStrings__Default="mongodb://admin:admin@host.docker.internal:27017/elsa_ddd_db?authSource=admin" `
  -p 14740:80 `
  elsaworkflows/elsa-server-and-studio-v3

# Wait for Elsa to be ready
Start-Sleep -Seconds 15

# Check status
docker ps --format "table {{.Names}}\t{{.Ports}}\t{{.Status}}"

Write-Host "✅ Setup Complete!" -ForegroundColor Green
Write-Host "🎨 Elsa Studio: http://localhost:14740" -ForegroundColor Cyan
Write-Host "📚 API Test: http://localhost:5158/api/WorkflowTest/test" -ForegroundColor Cyan
```

### Startup Sequence

**Important**: Always start containers in this order:

1. **MongoDB** (`my-mongo` container)
2. **Elsa Studio + Server** (`elsa-studio-server` container)  
3. **Domain API** (`dotnet run` - optional)

### Common Commands

```powershell
# Check running containers
docker ps --format "table {{.Names}}\t{{.Ports}}\t{{.Status}}"

# View container logs
docker logs elsa-studio-server
docker logs my-mongo

# Stop all services
docker stop elsa-studio-server my-mongo
docker rm elsa-studio-server my-mongo

# Restart if needed
docker restart elsa-studio-server
```

### Testing the Setup

```powershell
# Test Elsa Studio - should show workflow designer
# Open: http://localhost:14740

# Access Swagger API Documentation
# Open: http://localhost:5158/swagger

# Create test fulfilment order (use your actual fulfilment controller)
curl -X POST http://localhost:5158/api/fulfilments `
  -H "Content-Type: application/json" `
  -d '{"customerName": "Test User", "items": ["Item1", "Item2"]}'
```

### Available API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | http://localhost:5158/api/fulfilments | Create fulfilment order |
| GET | http://localhost:5158/api/fulfilments/{id} | Get fulfilment order |
| DELETE | http://localhost:5158/api/fulfilments/{id} | Delete fulfilment order |

### Troubleshooting
```powershell
# Check if containers are running
docker ps

# View container logs for errors
docker logs elsa-studio-server
docker logs my-mongo

# Restart containers if needed
docker restart elsa-studio-server
```

**API Connection Issues:**
```powershell
# Check if API is responding
curl http://localhost:5158/api/WorkflowTest/test

# Check API logs for errors
# See console output where 'dotnet run' was executed
```

---

## 🔗 Elsa Workflows API Reference

### Official Documentation

| Resource | URL |
|----------|-----|
| Elsa API Reference | [docs.elsaworkflows.io/docs/guides/workflow-management-api](https://docs.elsaworkflows.io/docs/guides/workflow-management-api) |
| REST API Endpoints | [docs.elsaworkflows.io/docs/api/workflow-management](https://docs.elsaworkflows.io/docs/api/workflow-management) |
| Elsa Core Repository | [github.com/elsa-workflows/elsa-core](https://github.com/elsa-workflows/elsa-core) |
| API Examples | [github.com/elsa-workflows/elsa-core/tree/main/samples](https://github.com/elsa-workflows/elsa-core/tree/main/samples) |

### Key Elsa API Endpoints (when exposed)

**Workflow Definitions:**
- `GET /elsa/api/workflow-definitions` - List workflow definitions
- `POST /elsa/api/workflow-definitions` - Create workflow definition  
- `GET /elsa/api/workflow-definitions/{id}` - Get workflow definition
- `PUT /elsa/api/workflow-definitions/{id}` - Update workflow definition
- `DELETE /elsa/api/workflow-definitions/{id}` - Delete workflow definition

**Workflow Instances:**
- `GET /elsa/api/workflow-instances` - List workflow instances
- `POST /elsa/api/workflow-instances` - Create workflow instance
- `GET /elsa/api/workflow-instances/{id}` - Get workflow instance  
- `POST /elsa/api/workflow-instances/{id}/execute` - Execute workflow
- `POST /elsa/api/workflow-instances/{id}/cancel` - Cancel workflow

**Activity Types:**
- `GET /elsa/api/activity-types` - List available activity types
- `GET /elsa/api/activity-types/{type}` - Get activity type details

*Note: These endpoints are available when Elsa API is properly configured with `UseWorkflowsApi()`. Currently managed through Elsa Studio Docker container.*

---

## External References

| Resource | URL |
|----------|-----|
| Elsa Workflows documentation | [docs.elsaworkflows.io](https://docs.elsaworkflows.io/) |
| Elsa GitHub repository | [github.com/elsa-workflows/elsa-core](https://github.com/elsa-workflows/elsa-core) |
| Testcontainers for .NET | [dotnet.testcontainers.org](https://dotnet.testcontainers.org/) |
| Reqnroll (BDD framework) | [reqnroll.net](https://reqnroll.net/) |
| Polly resilience library | [github.com/App-vNext/Polly](https://github.com/App-vNext/Polly) |
| MongoDB .NET Driver | [mongodb.com/docs/drivers/csharp](https://www.mongodb.com/docs/drivers/csharp/) |
| .NET Central Package Management | [learn.microsoft.com/nuget/consume-packages/central-package-management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management) |
