namespace Elsa.Workflow.Functional.Tests.Support;

/// <summary>
/// Typed request models serialised into JSON for HTTP calls.
/// Mirror the API surface without depending on internal request types.
/// </summary>
internal sealed record CreateOrderRequest(
    Guid                  FulfilmentOrderId,
    string                StoreId,
    string                OrderId,
    TestOrderLineRequest[] OrderLines);

internal sealed record TestOrderLineRequest(
    int     OrderLineNo,
    string  ArticleId,
    decimal ExpectedQuantity,
    string  UnitOfMeasure,
    string? CustomerSupplyInstructions);
