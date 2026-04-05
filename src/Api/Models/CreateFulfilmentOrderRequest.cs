using System.ComponentModel.DataAnnotations;

namespace Elsa.Workflow.Api.Models;

public sealed class CreateFulfilmentOrderRequest
{
    [Required]
    public Guid FulfilmentOrderId { get; init; }

    [Required]
    public string StoreId { get; init; } = default!;

    [Required]
    public string OrderId { get; init; } = default!;

    [Required]
    [MinLength(1, ErrorMessage = "OrderLines cannot be empty.")]
    public List<OrderLineRequest> OrderLines { get; init; } = default!;
}

public sealed class OrderLineRequest
{
    [Required]
    public int OrderLineNo { get; init; }

    [Required]
    public string ArticleId { get; init; } = default!;

    [Required]
    public decimal ExpectedQuantity { get; init; }

    [Required]
    public string UnitOfMeasure { get; init; } = default!;   // "Ea" | "Kg"

    [MaxLength(256)]
    public string? CustomerSupplyInstructions { get; init; }
}
