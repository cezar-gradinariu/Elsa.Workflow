using Elsa.Workflow.Domain.Exceptions;
using Elsa.Workflow.Domain.Repositories;
using Elsa.Workflows.Management;
using Elsa.Workflows.Management.Filters;
using Elsa.Workflows.Runtime;

namespace Elsa.Workflow.Application.Commands;

public sealed class DeleteFulfilmentOrderCommandHandler(
    IFulfilmentOrderRepository repository,
    IWorkflowInstanceStore     workflowInstanceStore,
    IWorkflowRuntime           workflowRuntime)
    : ICommandHandler<DeleteFulfilmentOrderCommand>
{
    public async Task HandleAsync(DeleteFulfilmentOrderCommand command, CancellationToken ct = default)
    {
        var order = await repository.FindByIdAsync(command.FulfilmentOrderId, ct)
            ?? throw new FulfilmentOrderNotFoundException(command.FulfilmentOrderId);

        // Cancel any running workflow instance(s) tied to this fulfilment order
        // using the IWorkflowClient API (the direct CancelWorkflowAsync on the runtime is obsolete).
        var correlationId = command.FulfilmentOrderId.ToString();

        var instanceIds = await workflowInstanceStore.FindManyIdsAsync(
            new WorkflowInstanceFilter { CorrelationId = correlationId },
            ct);

        foreach (var instanceId in instanceIds)
        {
            var client = await workflowRuntime.CreateClientAsync(instanceId, ct);
            await client.CancelAsync(ct);
        }

        // Mark aggregate as cancelled, then remove from store.
        order.Cancel();
        await repository.DeleteAsync(command.FulfilmentOrderId, ct);
    }
}
