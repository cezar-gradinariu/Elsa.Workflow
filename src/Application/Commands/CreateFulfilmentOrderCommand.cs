using Elsa.Workflow.Domain.Enums;
using Elsa.Workflow.Domain.ValueObjects;

namespace Elsa.Workflow.Application.Commands;

public sealed record CreateFulfilmentOrderCommand(
    FulfilmentOrderId             FulfilmentOrderId,
    StoreId                       StoreId,
    OrderId                       OrderId,
    IReadOnlyList<OrderLineInput> OrderLines
);

public sealed record OrderLineInput(
    int           OrderLineNo,
    string        ArticleId,
    decimal       ExpectedQuantity,
    UnitOfMeasure UnitOfMeasure,
    string?       CustomerSupplyInstructions
);
