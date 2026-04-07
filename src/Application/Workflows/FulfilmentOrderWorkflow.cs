using Elsa.Workflows;
using Elsa.Workflows.Activities;

namespace Elsa.Workflow.Application.Workflows;

/// <summary>
/// Orchestrates the full lifecycle of a FulfilmentOrder.
///
/// Current scope (v1):
///   - Workflow is started with FulfilmentOrderId as the only input variable.
///   - The aggregate is NEVER stored in workflow execution state (ADR-010).
///     Activities load it from IFulfilmentOrderRepository when needed.
///
/// Planned next step (ADR-013):
///   - CallOfaActivity: calls the OrderFulfilmentAllocator HTTP API with
///     orderId, storeId, and orderLines loaded from the aggregate.
///   - On success: calls FulfilmentOrder.ApplyAllocation(...), persists aggregate,
///     then suspends via a bookmark awaiting further triggers.
/// </summary>
public class FulfilmentOrderWorkflow : WorkflowBase
{
    public const string DefinitionId         = "fulfilment-order-workflow";
    public const string FulfilmentOrderIdVar = "FulfilmentOrderId";

    protected override void Build(IWorkflowBuilder workflow)
    {
        // IWorkflowBuilder exposes DefinitionId and Version as settable properties.
        workflow.DefinitionId = DefinitionId;
        workflow.Version = 1;

        // FulfilmentOrderId is the only data carried in workflow state.
        // Activities resolve the aggregate from the repository using this ID.
        workflow.WithVariable<string>(FulfilmentOrderIdVar);

        // TODO (ADR-013): Replace this placeholder with the full activity sequence:
        //
        //   new Sequence
        //   {
        //       Activities =
        //       [
        //           new CallOfaActivity(),         // Resilient OFA HTTP call
        //           new ApplyAllocationActivity(),  // Updates aggregate via domain method
        //           new SuspendFulfilmentActivity(),// Bookmark — awaits next trigger
        //       ]
        //   }
        //
        // CallOfaActivity uses the named HttpClient("OFA") registered in Infrastructure
        // with Polly retry + circuit breaker (ADR-006).
        workflow.Root = new Sequence
        {
            Activities = 
            [
                // Placeholder activity so the workflow appears in Elsa Studio
                new WriteLine("FulfilmentOrderWorkflow started - this workflow is now visible in Elsa Studio!")
            ]
        };
    }
}
