using Elsa.Workflow.Domain.Aggregates;

namespace Elsa.Workflow.Api.Models;

public sealed record FulfilmentOrderResponse(
    Guid                    FulfilmentOrderId,
    string                  StoreId,
    string                  OrderId,
    string                  Status,
    int                     Version,
    List<OrderLineResponse> OrderLines
)
{
    public static FulfilmentOrderResponse From(FulfilmentOrder order) =>
        new(
            FulfilmentOrderId: order.Id.Value,
            StoreId:           order.StoreId.Value,
            OrderId:           order.OrderId.Value,
            Status:            order.Status.ToString(),
            Version:           order.Version,
            OrderLines:        order.OrderLines.Select(OrderLineResponse.From).ToList()
        );
}

public sealed record OrderLineResponse(
    int                          OrderLineNo,
    string                       ArticleId,
    decimal                      ExpectedQuantity,
    string                       UnitOfMeasure,
    string?                      CustomerSupplyInstructions,
    string                       AllocationStatus,
    List<SubStoreAllocationResponse> Allocations
)
{
    public static OrderLineResponse From(Domain.Entities.OrderLine line) =>
        new(
            OrderLineNo:               line.OrderLineNo,
            ArticleId:                 line.ArticleId,
            ExpectedQuantity:          line.ExpectedQuantity,
            UnitOfMeasure:             line.UnitOfMeasure.ToString(),
            CustomerSupplyInstructions: line.CustomerSupplyInstructions,
            AllocationStatus:          line.AllocationStatus.ToString(),
            Allocations:               line.Allocations
                .Select(a => new SubStoreAllocationResponse(a.SubStoreId, a.AllocatedQuantity))
                .ToList()
        );
}

public sealed record SubStoreAllocationResponse(string SubStoreId, decimal AllocatedQuantity);
