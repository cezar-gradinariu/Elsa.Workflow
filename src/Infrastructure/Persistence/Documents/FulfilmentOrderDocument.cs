using MongoDB.Bson.Serialization.Attributes;

namespace Elsa.Workflow.Infrastructure.Persistence.Documents;

/// <summary>
/// MongoDB document shape for the <c>fulfilmentOrders</c> collection.
/// Kept in Infrastructure — the aggregate is never serialized directly.
/// </summary>
internal sealed record FulfilmentOrderDocument
{
    [BsonId]
    public string Id { get; init; } = default!;

    public string                  StoreId    { get; init; } = default!;
    public string                  OrderId    { get; init; } = default!;
    public List<OrderLineDocument> OrderLines { get; init; } = [];
    public string                  Status     { get; init; } = default!;

    /// <summary>
    /// Incremented on every write. Used for optimistic concurrency (ADR-003).
    /// </summary>
    public int Version { get; init; }
}

internal sealed class OrderLineDocument
{
    public int                        OrderLineNo                { get; init; }
    public string                     ArticleId                  { get; init; } = default!;
    public decimal                    ExpectedQuantity           { get; init; }
    public string                     UnitOfMeasure              { get; init; } = default!;
    public string?                    CustomerSupplyInstructions { get; init; }
    public string                     AllocationStatus           { get; init; } = "Pending";
    public List<SubStoreAllocationDocument> Allocations          { get; init; } = [];
}

internal sealed class SubStoreAllocationDocument
{
    public string  SubStoreId        { get; init; } = default!;
    public decimal AllocatedQuantity { get; init; }
}
