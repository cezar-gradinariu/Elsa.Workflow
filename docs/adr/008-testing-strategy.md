# ADR-008: Testing Strategy

**Status:** Accepted  
**Date:** 2026-04-03

## Context

The solution spans four source projects with different dependency profiles. Tests must cover each layer in isolation, integration between layers, and the full system end-to-end as a black box. The test suite must be runnable in CI without external service dependencies being pre-provisioned.

## Decision

Use four test projects, each targeting a different test level:

| Project | Type | What is tested |
|---------|------|----------------|
| `Domain.Tests` | Unit | Aggregates, value objects, domain services — pure in-memory |
| `Application.Tests` | Unit | Use-case handlers, Elsa activities — mocked infrastructure interfaces |
| `Integration.Tests` | Integration | Repository implementations, full stack via `WebApplicationFactory` |
| `Functional.Tests` | Functional / BDD | Full system black-box via HTTP — see below |

**Domain.Tests:**
- No mocks, no containers — Domain has zero external dependencies.
- Fast; run on every commit.

**Application.Tests:**
- Mock `IRepository<T>`, `IWorkflowDispatcher`, `IOutboxWriter`, and other infrastructure interfaces.
- Use Elsa's `WorkflowRunner` test helpers for activity-level tests.

**Integration.Tests:**
- Spin up MongoDB via Testcontainers (see [ADR-009](009-testcontainers-policy.md)).
- Use `WebApplicationFactory<Program>` for API-level integration tests.
- Verify repository implementations against a real MongoDB instance.
- Enforce dependency rules (ArchUnit-style layer tests live here).

**Functional.Tests — BDD black-box:**
- Framework: **Reqnroll** (SpecFlow successor for .NET).
- Runner binding: `Reqnroll.xUnit` or `Reqnroll.NUnit` (team choice).
- Infrastructure: `WebApplicationFactory<Program>` with real Kestrel port + Testcontainers MongoDB.
- **The API is the black box** — step definitions call HTTP endpoints only. No domain or application assemblies are referenced in step code.

```csharp
// Functional.Tests/Infrastructure/AppFixture.cs
public class AppFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _mongo = new MongoDbBuilder().Build();
    public HttpClient Client { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();
        var app = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(host =>
            {
                host.UseSetting("MongoDB:ConnectionString", _mongo.GetConnectionString());
                host.UseSetting("MongoDB:DatabaseName", "functional_tests");
            });
        Client = app.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await _mongo.DisposeAsync();
    }
}
```

**Feature file layout:**

```
Functional.Tests/
├── Features/
│   └── {BoundedContext}/
│       └── {Feature}.feature
├── Steps/
│   └── {Feature}Steps.cs
└── Infrastructure/
    ├── AppFixture.cs
    └── ReqnrollHooks.cs    ← [BeforeTestRun] / [AfterTestRun] container lifecycle
```

**Example scenario:**

```gherkin
Feature: Order Fulfilment
  Scenario: Successfully place a valid order
    Given the system is running and MongoDB is empty
    When I POST a valid order to "/api/orders"
    Then the response status is 202 Accepted
    And the order status eventually becomes "Confirmed"
```

**Constraints:**
- Scenario isolation: reset relevant MongoDB collections in `[BeforeScenario]` — do not restart containers per scenario.
- Authentication: use a fixed test JWT signed with a known secret injected via `appsettings.Testing.json`.
- Parallelism: feature files run in parallel; scenarios within a feature run sequentially.

## Consequences

**Positive:**
- Each test level has a clear, narrow scope — failures point directly at the layer that broke.
- Functional tests provide living documentation of system behaviour via Gherkin scenarios.
- No external services need to be pre-provisioned — Testcontainers manages everything.

**Negative:**
- Testcontainers requires Docker available in CI — must be accounted for in the pipeline configuration.
- Functional tests are the slowest tier — container startup overhead means they should run in a separate CI stage from unit tests.
- Reqnroll step bindings can become a maintenance burden as the scenario count grows — keep steps generic and shared where possible.
