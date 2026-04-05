using Elsa.Workflow.Domain.Aggregates;
using Elsa.Workflow.Domain.ValueObjects;

namespace Elsa.Workflow.Domain.Repositories;

public interface IFulfilmentOrderRepository
{
    /// <summary>
    /// Inserts a new aggregate. Throws if the ID already exists.
    /// </summary>
    Task AddAsync(FulfilmentOrder order, CancellationToken ct = default);

    /// <summary>
    /// Returns null if not found.
    /// </summary>
    Task<FulfilmentOrder?> FindByIdAsync(FulfilmentOrderId id, CancellationToken ct = default);

    /// <summary>
    /// Saves changes to an existing aggregate using the Version field for
    /// optimistic concurrency. Throws <see cref="Domain.Exceptions.OptimisticConcurrencyException"/>
    /// if the stored version no longer matches.
    /// </summary>
    Task UpdateAsync(FulfilmentOrder order, CancellationToken ct = default);

    /// <summary>
    /// Hard-deletes an aggregate by ID.
    /// </summary>
    Task DeleteAsync(FulfilmentOrderId id, CancellationToken ct = default);
}
