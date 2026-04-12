namespace Elsa.Workflow.Application.Contracts.Ofa;

/// <summary>
/// HTTP request body sent to the Order Fulfilment Allocator (OFA) API.
/// Mapped from the FulfilmentOrder aggregate inside CallOfaActivity — the Domain
/// type never crosses the HTTP boundary.
/// </summary>
public record OfaAllocationRequest(
    string                          OrderId,
    string                          StoreId,
    IReadOnlyList<OfaOrderLineRequest> OrderLines
);

public record OfaOrderLineRequest(
    int     OrderLineNo,
    string  ArticleId,
    decimal ExpectedQuantity,
    string  UnitOfMeasure
);
