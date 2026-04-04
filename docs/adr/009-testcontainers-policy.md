# ADR-009: Testcontainers Module Policy

**Status:** Accepted  
**Date:** 2026-04-03

## Context

Tests across `Integration.Tests` and `Functional.Tests` spin up Docker containers. Without a consistent policy, teams may use outdated versions, mix generic `ContainerBuilder` with official modules, or let versions drift between projects — causing subtle differences in test behaviour and maintenance overhead.

## Decision

**Always prefer an official Testcontainers module over a generic `ContainerBuilder` implementation. Use modules at their latest stable version.**

**Selection order when introducing a new containerised dependency:**

1. Check the [Testcontainers for .NET modules list](https://dotnet.testcontainers.org/modules/).
2. If a module exists — use it at its **latest stable version**. Never pin to an older version without a documented reason in the relevant ADR or PR.
3. If no module exists — use `ContainerBuilder` with an official Docker Hub image. Pin to an **explicit image tag** (e.g. `mongo:7.0`). Never use `latest` as a tag for generic containers — it is non-deterministic and cannot be reviewed in a PR diff.

**Version discipline — use Central Package Management:**

Declare all Testcontainers versions once in `Directory.Packages.props` at the solution root:

```xml
<Project>
  <ItemGroup>
    <PackageVersion Include="Testcontainers"        Version="4.11.0" />
    <PackageVersion Include="Testcontainers.MongoDb" Version="4.11.0" />
  </ItemGroup>
</Project>
```

Individual test projects reference packages without a version:

```xml
<PackageReference Include="Testcontainers.MongoDb" />
```

**Version sync rule:** the base `Testcontainers` package and all module packages must share the same version. Module packages declare a `>= x.y.z` dependency on the base — mismatches cause restore warnings or runtime errors. Upgrade all together in one PR.

**Current module map (as of 2026-04-03):**

| Technology | Package | Version | Notes |
|------------|---------|---------|-------|
| *(base)* | `Testcontainers` | 4.11.0 | Must match all module versions |
| MongoDB | `Testcontainers.MongoDb` | 4.11.0 | Official module |
| Redis *(if added)* | `Testcontainers.Redis` | latest at introduction | Official module exists |
| RabbitMQ *(if added)* | `Testcontainers.RabbitMq` | latest at introduction | Official module exists |
| Kafka *(if added)* | `Testcontainers.Kafka` | latest at introduction | Official module exists |
| API host | `WebApplicationFactory<Program>` | n/a | In-process — no container module needed |

## Consequences

**Positive:**
- Official modules handle image selection, port mapping, and readiness checks — less boilerplate, fewer test flakiness issues.
- Central Package Management prevents version drift between `Integration.Tests` and `Functional.Tests`.
- Explicit version pinning in `Directory.Packages.props` makes upgrades visible in PR diffs and easy to review.

**Negative:**
- Official modules may lag behind the latest Docker image version for a technology — check if the module's bundled image version is acceptable before adopting.
- Central Package Management requires all projects in the solution to opt in (`<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` in `Directory.Build.props`).
