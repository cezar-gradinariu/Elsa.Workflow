using Elsa.Workflow.Domain.ValueObjects;

namespace Elsa.Workflow.Domain.Exceptions;

public sealed class FulfilmentOrderAlreadyExistsException : DomainException
{
    public FulfilmentOrderAlreadyExistsException(FulfilmentOrderId id)
        : base($"A fulfilment order with ID '{id}' already exists.") { }
}

public sealed class FulfilmentOrderMustHaveOrderLinesException : DomainException
{
    public FulfilmentOrderMustHaveOrderLinesException(FulfilmentOrderId id)
        : base($"Fulfilment order '{id}' must contain at least one order line.") { }
}

public sealed class OrderLineNegativeQuantityException : DomainException
{
    public OrderLineNegativeQuantityException(FulfilmentOrderId id, IEnumerable<int> lineNumbers)
        : base($"Fulfilment order '{id}' has order lines with negative quantities: {string.Join(", ", lineNumbers)}.") { }
}

public sealed class DuplicateOrderLineNumbersException : DomainException
{
    public DuplicateOrderLineNumbersException(FulfilmentOrderId id, IEnumerable<int> duplicateLineNumbers)
        : base($"Fulfilment order '{id}' contains duplicate order line numbers: {string.Join(", ", duplicateLineNumbers)}.") { }
}

public sealed class FulfilmentOrderNotFoundException : DomainException
{
    public FulfilmentOrderNotFoundException(FulfilmentOrderId id)
        : base($"Fulfilment order '{id}' was not found.") { }
}

public sealed class AllocationMissingOrderLinesException : DomainException
{
    public AllocationMissingOrderLinesException(FulfilmentOrderId id, IEnumerable<int> missingLineNumbers)
        : base($"Allocation for fulfilment order '{id}' is missing order lines: {string.Join(", ", missingLineNumbers)}.") { }
}

public sealed class AllocationQuantityMismatchException : DomainException
{
    public AllocationQuantityMismatchException(FulfilmentOrderId id, int orderLineNo, decimal expected, decimal actual)
        : base($"Allocation quantity mismatch on fulfilment order '{id}', line {orderLineNo}: expected {expected}, allocated {actual}.") { }
}

public sealed class AllocationNegativeQuantityException : DomainException
{
    public AllocationNegativeQuantityException(FulfilmentOrderId id, int orderLineNo, string subStoreId)
        : base($"Negative allocated quantity on fulfilment order '{id}', line {orderLineNo}, sub-store '{subStoreId}'.") { }
}
