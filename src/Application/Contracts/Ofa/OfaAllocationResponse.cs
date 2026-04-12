namespace Elsa.Workflow.Application.Contracts.Ofa;

/// <summary>
/// HTTP response body received from the OFA API (Interpretation A — fully allocated).
/// Mapped to <see cref="Elsa.Workflow.Domain.ValueObjects.OrderAllocationResult"/> before
/// passing to the aggregate — the external contract never leaks into the Domain.
/// </summary>
public record OfaAllocationResponse(
    string                                  FulfilmentOrderId,
    IReadOnlyList<OfaSubStoreAllocation>    SubStoreAllocations
);

public record OfaSubStoreAllocation(
    string                          SubStoreId,
    IReadOnlyList<OfaSubStoreLine>  Lines
);

public record OfaSubStoreLine(
    int     OrderLineNo,
    string  ArticleId,
    decimal AllocatedQuantity,
    string  UnitOfMeasure
);
