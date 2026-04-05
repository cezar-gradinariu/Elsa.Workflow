using Elsa.Workflow.Application.Workflows;
using Elsa.Workflow.Domain.Aggregates;
using Elsa.Workflow.Domain.Entities;
using Elsa.Workflow.Domain.Repositories;
using Elsa.Workflows.Models;
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Messages;

namespace Elsa.Workflow.Application.Commands;

/// <summary>
/// Handles fulfilment order creation:
///   1. Builds the aggregate via the domain factory (DomainException on violation).
///   2. Persists the aggregate to MongoDB.
///   3. Starts the Elsa workflow via the IWorkflowClient API.
///
/// TRANSACTION NOTE:
///   Elsa's workflow runtime uses its own MongoDB session and cannot participate in
///   a custom MongoDB multi-document transaction. Two options are available:
///
///   Option A (current — accept eventual consistency):
///     The aggregate is saved first. If the client call fails, the aggregate exists
///     without a workflow. A background reconciliation job (future work) detects orphans
///     and retries the start. The window of inconsistency is small and bounded.
///
///   Option B (outbox — stronger guarantee, consistent with ADR-005):
///     Save the aggregate + an OutboxMessage("StartFulfilmentWorkflow") atomically in
///     one MongoDB session. The OutboxDispatcherService calls the client API later.
///     No orphan window. Adds outbox complexity.
///
///   Choose Option B if crash-safe exactly-once workflow start is required.
/// </summary>
public sealed class CreateFulfilmentOrderCommandHandler(
    IFulfilmentOrderRepository repository,
    IWorkflowRuntime           workflowRuntime)
    : ICommandHandler<CreateFulfilmentOrderCommand>
{
    public async Task HandleAsync(CreateFulfilmentOrderCommand command, CancellationToken ct = default)
    {
        var orderLines = command.OrderLines
            .Select(l => OrderLine.Create(
                l.OrderLineNo,
                l.ArticleId,
                l.ExpectedQuantity,
                l.UnitOfMeasure,
                l.CustomerSupplyInstructions))
            .ToList()
            .AsReadOnly();

        var order = FulfilmentOrder.Create(
            command.FulfilmentOrderId,
            command.StoreId,
            command.OrderId,
            orderLines);

        // Option A: save aggregate first, then start workflow.
        await repository.AddAsync(order, ct);

        // Use the current (non-obsolete) IWorkflowClient API.
        var client = await workflowRuntime.CreateClientAsync(ct);

        await client.CreateAndRunInstanceAsync(
            new CreateAndRunWorkflowInstanceRequest
            {
                WorkflowDefinitionHandle = WorkflowDefinitionHandle.ByDefinitionId(
                    FulfilmentOrderWorkflow.DefinitionId),
                CorrelationId = command.FulfilmentOrderId.ToString(),
                Input = new Dictionary<string, object>
                {
                    [FulfilmentOrderWorkflow.FulfilmentOrderIdVar] = command.FulfilmentOrderId.Value.ToString()
                }
            },
            ct);
    }
}
