using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Memory;
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

        // Variable<T>(string name, T defaultValue) is the correct named constructor.
        // WithVariable<T>(string) resolves to WithVariable<T>(T defaultValue) — the string
        // becomes the DEFAULT VALUE, not the name. Always use new Variable<T> explicitly.
        var fulfilmentOrderIdVar = new Variable<string>(FulfilmentOrderIdVar, null!);
        var apiResponseVar       = new Variable<string>("ApiResponse",        null!);

        workflow.WithVariable(fulfilmentOrderIdVar);
        workflow.WithVariable(apiResponseVar);

        workflow.Root = new Sequence
        {
            Activities =
            [
                new WorkflowStartedActivity(),

                new Delay(TimeSpan.FromSeconds(10)),

                new CallExternalApiActivity
                {
                    Url = new Input<string>("https://jsonplaceholder.typicode.com/posts/1"),
                    // Wire the output to the workflow variable so its value persists in
                    // workflow instance state and is visible in Elsa Studio's Variables panel.
                    Result = new Output<string>(apiResponseVar)
                },

                new ApiCallCompletedActivity
                {
                    // Cast to non-generic Variable to hit the public Input<T>(Variable) constructor.
                    // Input<T>(Variable<T>) resolves to the protected MemoryBlockReference overload.
                    ApiResponse = new Input<string>((Variable)apiResponseVar)
                },

                new WorkflowCompletedActivity()
            ]
        };
    }
}
