namespace Elsa.Workflow.Domain.ValueObjects;

/// <summary>
/// The latest preparation report received from a substore for a specific Prepare command.
///
/// Last-write-wins: a newer report for the same <see cref="PrepareCommandId"/> replaces
/// the previous one on the aggregate (see <see cref="Aggregates.FulfilmentOrder.RegisterContainers"/>).
/// </summary>
public sealed record SubStorePrepareReport(
    string PrepareCommandId,
    string SubStoreId,
    IReadOnlyList<Container> Containers,
    DateTime ReceivedAt);
