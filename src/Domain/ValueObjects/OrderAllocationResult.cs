namespace Elsa.Workflow.Domain.ValueObjects;

/// <summary>
/// Domain representation of the OFA allocation response, passed to
/// <see cref="Elsa.Workflow.Domain.Aggregates.FulfilmentOrder.ApplyAllocation"/>.
///
/// The Application layer (CallOfaActivity) maps the raw OFA HTTP response to this type
/// before calling the aggregate method — keeping the external contract out of the Domain.
/// </summary>
public sealed record OrderAllocationResult(
    IReadOnlyList<LineAllocation> Lines
);

/// <summary>
/// All sub-store allocations for a single order line.
/// </summary>
public sealed record LineAllocation(
    int                              OrderLineNo,
    IReadOnlyList<SubStoreAllocation> SubStoreAllocations
);
