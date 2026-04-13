using Elsa.Extensions;
using Elsa.Workflow.Application.Activities;
using Elsa.Workflows;
using Elsa.Workflows.Activities;

namespace Elsa.Workflow.Application.Workflows;

/// <summary>
/// Orchestrates the full lifecycle of a FulfilmentOrder.
///
/// The aggregate is NEVER stored in workflow execution state (ADR-010).
/// Only scalar IDs and the PrepareCommandMap travel in workflow variables;
/// activities load the aggregate from IFulfilmentOrderRepository when needed.
///
/// Flow:
///   1. Copy FulfilmentOrderId from Input into the workflow variable.
///   2. CallOfaActivity  — calls the OFA HTTP endpoint, applies allocation, persists aggregate.
///   3. ManagePrepareActivity — fan-out: for each allocated substore:
///        a. Publish PrepareSubstoreCommand to Service Bus (with a stable PrepareCommandId).
///        b. Register a bookmark keyed on PrepareCommandId — workflow suspends here.
///        c. On PrepareReport arrival: update aggregate (last-write-wins), re-suspend.
///        (This activity never completes — the workflow remains alive indefinitely.)
///
/// Resilience (ADR-004 / ADR-006):
///   CallOfaActivity uses Elsa.Resilience (IResilientActivityInvoker + OfaResilienceStrategy).
///   If Level-1 exhausts, the workflow reaches Faulted state and an operator can resume
///   via the Elsa Alterations API (ADR-013 / OQ-3).
/// </summary>
public class FulfilmentOrderWorkflow : WorkflowBase
{
    public const string DefinitionId         = "fulfilment-order-workflow";
    public const string FulfilmentOrderIdVar = "FulfilmentOrderId";
    public const string PrepareCommandMapVar = "PrepareCommandMap";

    protected override void Build(IWorkflowBuilder workflow)
    {
        workflow.DefinitionId = DefinitionId;
        workflow.Version      = 1;

        // Declare workflow variables — the two-parameter overload (name, defaultValue)
        // names the variable so SetVariable/GetVariable extension methods resolve it by name.
        workflow.WithVariable<string>(FulfilmentOrderIdVar, default!);
        workflow.WithVariable<Dictionary<string, string>>(PrepareCommandMapVar, new Dictionary<string, string>());

        workflow.Root = new Sequence
        {
            Activities =
            [
                // Elsa does not auto-map CreateAndRunWorkflowInstanceRequest.Input to
                // workflow variables — it lands in WorkflowExecutionContext.Input.
                // Copy it into the declared variable here so all downstream activities
                // can use context.GetVariable<string>(FulfilmentOrderIdVar).
                new Inline(ctx =>
                {
                    var input = ctx.WorkflowExecutionContext.Input;
                    if (input?.TryGetValue(FulfilmentOrderIdVar, out var val) == true)
                        ctx.SetVariable(FulfilmentOrderIdVar, val?.ToString());
                    return ValueTask.CompletedTask;
                }),

                // Step 2: Call OFA → allocate lines to substores → persist aggregate.
                new CallOfaActivity(),

                // Step 3: Fan-out Prepare phase.
                // Publishes one Service Bus message per substore, then suspends on N bookmarks.
                // Each PrepareReport resumes the relevant bookmark, updates the aggregate,
                // and re-suspends — keeping the workflow alive indefinitely.
                new ManagePrepareActivity(),
            ]
        };
    }
}
