using Elsa.Workflow.Domain.Enums;
using Elsa.Workflow.Domain.ValueObjects;

namespace Elsa.Workflow.Domain.Entities;

public class OrderLine
{
    public int                           OrderLineNo                { get; private set; }
    public string                        ArticleId                  { get; private set; } = default!;
    public decimal                       ExpectedQuantity           { get; private set; }
    public UnitOfMeasure                 UnitOfMeasure              { get; private set; }
    public string?                       CustomerSupplyInstructions { get; private set; }

    public AllocationStatus              AllocationStatus           { get; private set; } = AllocationStatus.Pending;
    public IReadOnlyList<SubStoreAllocation> Allocations            { get; private set; } = [];

    private OrderLine() { }

    public static OrderLine Create(
        int           orderLineNo,
        string        articleId,
        decimal       expectedQuantity,
        UnitOfMeasure unitOfMeasure,
        string?       customerSupplyInstructions = null)
    {
        if (string.IsNullOrWhiteSpace(articleId))
            throw new ArgumentException("ArticleId cannot be null or whitespace.", nameof(articleId));

        if (customerSupplyInstructions?.Length > 256)
            throw new ArgumentException("CustomerSupplyInstructions cannot exceed 256 characters.", nameof(customerSupplyInstructions));

        return new OrderLine
        {
            OrderLineNo                = orderLineNo,
            ArticleId                  = articleId.Trim(),
            ExpectedQuantity           = expectedQuantity,
            UnitOfMeasure              = unitOfMeasure,
            CustomerSupplyInstructions = customerSupplyInstructions
        };
    }

    /// <summary>
    /// Called by <see cref="Elsa.Workflow.Domain.Aggregates.FulfilmentOrder.ApplyAllocation"/>
    /// after the aggregate has validated the overall allocation result.
    /// Internal to keep mutation inside the Domain assembly.
    /// </summary>
    internal void SetAllocations(IReadOnlyList<SubStoreAllocation> allocations)
    {
        Allocations      = allocations;
        AllocationStatus = AllocationStatus.FullyAllocated;
    }

    /// <summary>
    /// Reconstructs an OrderLine from its persisted state, including allocation data.
    /// For use by the repository only — not for domain logic.
    /// </summary>
    public static OrderLine Rehydrate(
        int                              orderLineNo,
        string                           articleId,
        decimal                          expectedQuantity,
        UnitOfMeasure                    unitOfMeasure,
        string?                          customerSupplyInstructions,
        AllocationStatus                 allocationStatus,
        IReadOnlyList<SubStoreAllocation> allocations)
    {
        var line = new OrderLine
        {
            OrderLineNo                = orderLineNo,
            ArticleId                  = articleId,
            ExpectedQuantity           = expectedQuantity,
            UnitOfMeasure              = unitOfMeasure,
            CustomerSupplyInstructions = customerSupplyInstructions,
            AllocationStatus           = allocationStatus,
            Allocations                = allocations
        };
        return line;
    }
}
