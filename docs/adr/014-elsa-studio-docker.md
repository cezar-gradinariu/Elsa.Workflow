# ADR-014: Elsa Studio via Official Docker Image

**Status:** Accepted  
**Date:** 2026-04-07  
**Supersedes:** ADR-007

## Context

The ops team needs a visual interface to monitor workflow instances, inspect incidents, manually retry or skip faulted activities, and manage workflow definitions. After evaluating embedded and standalone approaches, we determined that using the **official Elsa Studio Docker image** provides the best balance of simplicity and maintainability.

## Decision

**Use the official Elsa Studio Docker image that connects to our API over HTTP.**

### Architecture

```
┌─────────────────┐    HTTP/HTTPS     ┌──────────────────┐
│  Elsa Studio    │ ◄───────────────► │   Your API       │
│  (Docker)       │    /elsa/api      │   (localhost)    │
│  localhost:14740│                   │   localhost:7001 │
└─────────────────┘                   └──────────────────┘
```

### API Project Configuration

**Elsa Configuration:**
```csharp
builder.Services.AddElsa(elsa =>
{
    elsa.UseElsaMongoDb(connectionString, databaseName);
    elsa.AddWorkflowsFrom<FulfilmentOrderWorkflow>();
    elsa.UseWorkflowManagement(); // Enables Studio API endpoints
});
```

**CORS for Studio:**
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("ElsaStudio", policy =>
        policy.WithOrigins("http://localhost:14740", "https://localhost:14740")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});
```

**API Endpoint Mapping:**
```csharp
app.MapElsaApiEndpoints(); // Exposes workflow management APIs
```

## Consequences

**Positive:**
- **Zero package management** - No Elsa Studio dependencies in our codebase
- **Official support** - Always up-to-date with latest Elsa Studio features
- **Clean separation** - Studio failures don't affect our API
- **Easy deployment** - Single Docker command to run Studio
- **Production ready** - Official image is tested and maintained by Elsa team

**Negative:**
- **Docker dependency** - Requires Docker or container runtime
- **Network dependency** - Studio must reach API over HTTP
- **Additional container** - One more service to manage in production

## Usage

### Development
```bash
# Start your API
dotnet run --project src/Api

# Start Elsa Studio (Docker)
docker run -t -i -e ASPNETCORE_ENVIRONMENT=Development -e ASPNETCORE_URLS=http://+:80 -e Elsa__Server__BaseUrl=http://host.docker.internal:7001 -p 14740:80 elsaworkflows/elsa-studio-and-server:latest
```

### Production
- Configure Studio container with production API URL
- Ensure proper network policies allow Studio → API communication
- Consider running behind same reverse proxy/load balancer

## Security Considerations

- API endpoints under `/elsa/api` should be secured appropriately for production
- CORS policy should be restricted to actual Studio URLs in production
- Network isolation and proper firewall rules should be implemented