namespace Elsa.Workflow.Domain.ValueObjects;

/// <summary>
/// Records how much of an order line's quantity was allocated to a specific sub-store.
/// </summary>
public sealed record SubStoreAllocation(string SubStoreId, decimal AllocatedQuantity);
