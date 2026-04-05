using Elsa.Workflow.Domain.Entities;
using Elsa.Workflow.Domain.Enums;
using Elsa.Workflow.Domain.Exceptions;
using Elsa.Workflow.Domain.ValueObjects;

namespace Elsa.Workflow.Domain.Aggregates;

public class FulfilmentOrder
{
    public FulfilmentOrderId        Id          { get; private set; }
    public StoreId                  StoreId     { get; private set; }
    public OrderId                  OrderId     { get; private set; }
    public IReadOnlyList<OrderLine> OrderLines  { get; private set; } = default!;
    public FulfilmentOrderStatus    Status      { get; private set; }

    /// <summary>
    /// Optimistic concurrency version. Starts at 0 when first created;
    /// the repository increments it on every update via a conditional
    /// { version: N } filter — see IFulfilmentOrderRepository.
    /// </summary>
    public int Version { get; private set; }

    // Required by MongoDB driver for deserialization.
    private FulfilmentOrder() { }

    /// <summary>
    /// Creates a new, valid FulfilmentOrder aggregate.
    /// Throws a <see cref="DomainException"/> if any invariant is violated.
    /// </summary>
    public static FulfilmentOrder Create(
        FulfilmentOrderId        id,
        StoreId                  storeId,
        OrderId                  orderId,
        IReadOnlyList<OrderLine> orderLines)
    {
        if (orderLines is null || orderLines.Count == 0)
            throw new FulfilmentOrderMustHaveOrderLinesException(id);

        var negativeLines = orderLines
            .Where(l => l.ExpectedQuantity < 0)
            .Select(l => l.OrderLineNo)
            .ToList();

        if (negativeLines.Count > 0)
            throw new OrderLineNegativeQuantityException(id, negativeLines);

        var duplicateLineNumbers = orderLines
            .GroupBy(l => l.OrderLineNo)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateLineNumbers.Count > 0)
            throw new DuplicateOrderLineNumbersException(id, duplicateLineNumbers);

        return new FulfilmentOrder
        {
            Id         = id,
            StoreId    = storeId,
            OrderId    = orderId,
            OrderLines = orderLines,
            Status     = FulfilmentOrderStatus.Pending,
            Version    = 0
        };
    }

    /// <summary>
    /// Applies the OFA allocation result to the aggregate (Interpretation A — fully allocated).
    ///
    /// Validation rules:
    ///   1. Every original OrderLineNo must appear in <paramref name="result"/>.
    ///   2. No sub-store allocation may have a negative quantity.
    ///   3. SUM(allocatedQuantity) for each line must equal its ExpectedQuantity exactly.
    ///
    /// On success, each OrderLine's Allocations and AllocationStatus are updated,
    /// and the aggregate Status is set to Allocated.
    /// </summary>
    public void ApplyAllocation(OrderAllocationResult result)
    {
        // 1. Every original line must be covered.
        var resultByLine = result.Lines.ToDictionary(l => l.OrderLineNo);

        var missingLines = OrderLines
            .Select(l => l.OrderLineNo)
            .Where(n => !resultByLine.ContainsKey(n))
            .ToList();

        if (missingLines.Count > 0)
            throw new AllocationMissingOrderLinesException(Id, missingLines);

        // 2 & 3. Validate each line in the result.
        foreach (var orderLine in OrderLines)
        {
            var lineAllocation = resultByLine[orderLine.OrderLineNo];

            foreach (var sub in lineAllocation.SubStoreAllocations)
            {
                if (sub.AllocatedQuantity < 0)
                    throw new AllocationNegativeQuantityException(Id, orderLine.OrderLineNo, sub.SubStoreId);
            }

            var totalAllocated = lineAllocation.SubStoreAllocations.Sum(s => s.AllocatedQuantity);

            if (totalAllocated != orderLine.ExpectedQuantity)
                throw new AllocationQuantityMismatchException(
                    Id, orderLine.OrderLineNo, orderLine.ExpectedQuantity, totalAllocated);
        }

        // All validations passed — apply to each OrderLine.
        foreach (var orderLine in OrderLines)
            orderLine.SetAllocations(resultByLine[orderLine.OrderLineNo].SubStoreAllocations);

        Status = FulfilmentOrderStatus.Allocated;
    }

    public void Cancel()
    {
        if (Status == FulfilmentOrderStatus.Cancelled)
            return;

        Status = FulfilmentOrderStatus.Cancelled;
    }

    /// <summary>
    /// Reconstructs an aggregate from its persisted state (used by the repository only).
    /// Not intended for use in domain logic or application services.
    /// </summary>
    public static FulfilmentOrder Rehydrate(
        FulfilmentOrderId        id,
        StoreId                  storeId,
        OrderId                  orderId,
        IReadOnlyList<OrderLine> orderLines,
        FulfilmentOrderStatus    status,
        int                      version) =>
        new()
        {
            Id         = id,
            StoreId    = storeId,
            OrderId    = orderId,
            OrderLines = orderLines,
            Status     = status,
            Version    = version
        };
}
