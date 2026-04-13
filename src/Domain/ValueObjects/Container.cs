namespace Elsa.Workflow.Domain.ValueObjects;

/// <summary>
/// A physical container that holds picked order line quantities.
/// Received via a <see cref="SubStorePrepareReport"/> from the substore.
/// </summary>
public sealed record Container(
    string ContainerId,
    IReadOnlyList<ContainerLine> Lines);

/// <summary>
/// The portion of an order line placed into a specific container.
/// </summary>
public sealed record ContainerLine(int OrderLineNo, decimal Quantity);
