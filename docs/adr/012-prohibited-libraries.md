# ADR-012: Prohibited Third-Party Libraries

**Status:** Accepted  
**Date:** 2026-04-05

## Context

Several widely-used libraries carry commercial licences or licence terms that are incompatible with this project's distribution or usage constraints. To prevent accidental introduction of these dependencies, they are explicitly prohibited and this decision must be enforced in code review and tooling.

## Decision

The following NuGet packages **must not be added** to any project in this solution:

| Library | Affected packages | Reason for prohibition |
|---------|------------------|------------------------|
| **MediatR** | `MediatR`, `MediatR.Extensions.*` | Commercial licence introduced in v12+ (Jimmy Bogard's "MediatR.Contracts" is free but the pipeline is not). Replaced by hand-rolled command/query handler interfaces — see §Replacements. |
| **AutoMapper** | `AutoMapper`, `AutoMapper.Extensions.*` | Commercial licence introduced in v13+ for commercial use. Replaced by explicit mapping methods — see §Replacements. |
| **FluentAssertions** | `FluentAssertions` (v8+) | Commercial licence for companies introduced in v8. Replaced by `Shouldly` (MIT) or plain `Assert` in tests. |

### Replacements

**MediatR → Hand-rolled command/query handlers**

Define plain interfaces and concrete handler classes:

```csharp
// Application/Commands/ICommandHandler.cs
public interface ICommandHandler<TCommand>
{
    Task HandleAsync(TCommand command, CancellationToken ct);
}

public interface IQueryHandler<TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken ct);
}
```

Register directly in the DI container (no pipeline behaviour abstraction needed for v1):

```csharp
services.AddScoped<ICommandHandler<CreateFulfilmentOrderCommand>, CreateFulfilmentOrderCommandHandler>();
```

Controllers receive the handler directly by constructor injection — no mediator bus required.

**AutoMapper → Explicit static mapping methods**

Map between layers using dedicated static `Map` methods:

```csharp
// Api/Models/FulfilmentOrderResponse.cs
public record FulfilmentOrderResponse(...)
{
    public static FulfilmentOrderResponse From(FulfilmentOrder order) => new(
        FulfilmentOrderId: order.Id.Value,
        StoreId:           order.StoreId.Value,
        // ...
    );
}
```

Or mapping classes if complexity warrants:

```csharp
// Application/Mapping/FulfilmentOrderMapper.cs
public static class FulfilmentOrderMapper
{
    public static CreateFulfilmentOrderCommand ToCommand(this CreateFulfilmentOrderRequest request) => ...;
}
```

**FluentAssertions → Shouldly or plain Assert**

```csharp
// Shouldly (MIT licence) — install only in test projects
result.ShouldBe(expected);

// Or plain xUnit Assert:
Assert.Equal(expected, result);
Assert.Throws<DomainException>(() => FulfilmentOrder.Create(...));
```

### Enforcement

1. Add a `Directory.Build.props` or a Roslyn analyser that rejects the prohibited package IDs.
2. Flag violations in pull request review checklist.
3. CI: `dotnet list package` output inspected for prohibited package names in the build pipeline.

## Consequences

**Positive:**
- No licence risk from the named libraries.
- Command/query handlers without a mediator bus are simpler to trace — the call graph is explicit and directly injected.
- Mapping methods are trivially unit-testable and generate no reflection overhead.

**Negative:**
- Slightly more boilerplate than MediatR pipeline behaviours (cross-cutting concerns like logging must be wired per-handler or via a decorator pattern).
- Developers familiar with MediatR/AutoMapper need to learn the project's replacement conventions — these are documented in this ADR as the canonical reference.
