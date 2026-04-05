using Elsa.Workflow.Domain.Aggregates;
using Elsa.Workflow.Domain.Entities;
using Elsa.Workflow.Domain.Enums;
using Elsa.Workflow.Domain.Exceptions;
using Elsa.Workflow.Domain.Repositories;
using Elsa.Workflow.Domain.ValueObjects;
using Elsa.Workflow.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace Elsa.Workflow.Infrastructure.Repositories;

public sealed class FulfilmentOrderRepository : IFulfilmentOrderRepository
{
    private readonly IMongoCollection<FulfilmentOrderDocument> _collection;

    public FulfilmentOrderRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<FulfilmentOrderDocument>("fulfilmentOrders");
    }

    public async Task AddAsync(FulfilmentOrder order, CancellationToken ct = default)
    {
        var document = ToDocument(order);
        try
        {
            await _collection.InsertOneAsync(document, cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new FulfilmentOrderAlreadyExistsException(order.Id);
        }
    }

    public async Task<FulfilmentOrder?> FindByIdAsync(FulfilmentOrderId id, CancellationToken ct = default)
    {
        var filter = Builders<FulfilmentOrderDocument>.Filter.Eq(d => d.Id, id.Value.ToString());
        var document = await _collection.Find(filter).FirstOrDefaultAsync(ct);
        return document is null ? null : ToDomain(document);
    }

    public async Task UpdateAsync(FulfilmentOrder order, CancellationToken ct = default)
    {
        var document = ToDocument(order);

        // Optimistic concurrency: match on both _id and the current version.
        var filter = Builders<FulfilmentOrderDocument>.Filter.And(
            Builders<FulfilmentOrderDocument>.Filter.Eq(d => d.Id, document.Id),
            Builders<FulfilmentOrderDocument>.Filter.Eq(d => d.Version, order.Version));

        // Replace the entire document but with Version incremented by 1.
        var updated = document with { Version = document.Version + 1 };

        var result = await _collection.ReplaceOneAsync(filter, updated, cancellationToken: ct);

        if (result.ModifiedCount == 0)
            throw new OptimisticConcurrencyException(order.Id.ToString());
    }

    public async Task DeleteAsync(FulfilmentOrderId id, CancellationToken ct = default)
    {
        var filter = Builders<FulfilmentOrderDocument>.Filter.Eq(d => d.Id, id.Value.ToString());
        await _collection.DeleteOneAsync(filter, ct);
    }

    // -------------------------------------------------------------------------
    // Mapping helpers — explicit, no AutoMapper (ADR-012)
    // -------------------------------------------------------------------------

    private static FulfilmentOrderDocument ToDocument(FulfilmentOrder order) =>
        new()
        {
            Id         = order.Id.Value.ToString(),
            StoreId    = order.StoreId.Value,
            OrderId    = order.OrderId.Value,
            Status     = order.Status.ToString(),
            Version    = order.Version,
            OrderLines = order.OrderLines.Select(ToOrderLineDocument).ToList()
        };

    private static OrderLineDocument ToOrderLineDocument(OrderLine line) =>
        new()
        {
            OrderLineNo                = line.OrderLineNo,
            ArticleId                  = line.ArticleId,
            ExpectedQuantity           = line.ExpectedQuantity,
            UnitOfMeasure              = line.UnitOfMeasure.ToString(),
            CustomerSupplyInstructions = line.CustomerSupplyInstructions,
            AllocationStatus           = line.AllocationStatus.ToString(),
            Allocations                = line.Allocations
                .Select(a => new SubStoreAllocationDocument
                {
                    SubStoreId        = a.SubStoreId,
                    AllocatedQuantity = a.AllocatedQuantity
                })
                .ToList()
        };

    private static FulfilmentOrder ToDomain(FulfilmentOrderDocument doc)
    {
        var orderLines = doc.OrderLines
            .Select(l => OrderLine.Rehydrate(
                l.OrderLineNo,
                l.ArticleId,
                l.ExpectedQuantity,
                Enum.Parse<UnitOfMeasure>(l.UnitOfMeasure),
                l.CustomerSupplyInstructions,
                Enum.Parse<AllocationStatus>(l.AllocationStatus),
                l.Allocations
                    .Select(a => new SubStoreAllocation(a.SubStoreId, a.AllocatedQuantity))
                    .ToList()
                    .AsReadOnly()))
            .ToList()
            .AsReadOnly();

        // Rehydrate the aggregate using its private-setter properties via reflection-free
        // reconstruction: Create + version injection via a dedicated Rehydrate factory.
        return FulfilmentOrder.Rehydrate(
            FulfilmentOrderId.From(Guid.Parse(doc.Id)),
            StoreId.From(doc.StoreId),
            OrderId.From(doc.OrderId),
            orderLines,
            Enum.Parse<FulfilmentOrderStatus>(doc.Status),
            doc.Version);
    }
}
