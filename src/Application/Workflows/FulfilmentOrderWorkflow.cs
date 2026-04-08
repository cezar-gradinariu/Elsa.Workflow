using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Models;
using Elsa.Scheduling.Activities;
using Elsa.Workflow.Application.Workflows.Activities;

namespace Elsa.Workflow.Application.Workflows;

/// <summary>
/// Orchestrates the full lifecycle of a FulfilmentOrder.
///
/// Current scope (v1):
///   - Workflow is started with FulfilmentOrderId as the only input variable. 
///   - The aggregate is NEVER stored in workflow execution state (ADR-010).
///     Activities load it from IFulfilmentOrderRepository when needed.
///   - Includes a 1-minute delay before making an external API call.
///   - Calls https://jsonplaceholder.typicode.com/posts/1 after the delay for demonstration.
///   - Logs API call results and continues to completion.
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
                // Step 1: Initial workflow start notification  
                new WorkflowStartedActivity(),
                
                // Step 2: Prepare for 1-minute delay
                new PrepareDelayActivity(),
                new Delay(TimeSpan.FromSeconds(10)),
                
                // Step 3: Prepare for external API call
                new PrepareApiCallActivity(),
                new CallExternalApiActivity
                {
                    Url = new Input<string>("https://jsonplaceholder.typicode.com/posts/1")
                },
                new ApiCallCompletedActivity(),
                
                // Step 4: Begin finalization process
                new BeginFinalizationActivity(), 
                
                // Step 5: Mark workflow as completed  
                new WorkflowCompletedActivity()
            ]
        };
    }
}
