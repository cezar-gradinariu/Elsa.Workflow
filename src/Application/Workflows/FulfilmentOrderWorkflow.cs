using Elsa.Workflow.Application.Activities;
using Elsa.Workflows;
using Elsa.Workflows.Activities;

namespace Elsa.Workflow.Application.Workflows;

/// <summary>
/// Orchestrates the full lifecycle of a FulfilmentOrder.
///
/// The aggregate is NEVER stored in workflow execution state (ADR-010).
/// Only FulfilmentOrderId travels in workflow variables; activities load
/// the aggregate from IFulfilmentOrderRepository when needed.
///
/// Resilience (ADR-004 / ADR-006):
///   CallOfaActivity uses Elsa.Resilience (IResilientActivityInvoker +
///   OfaResilienceStrategy) for Level-1 fast retry + circuit breaker.
///   If Level-1 exhausts, the workflow reaches Faulted state and an
///   operator can resume via the Elsa Alterations API (ADR-013 / OQ-3).
/// </summary>
public class FulfilmentOrderWorkflow : WorkflowBase
{
    public const string DefinitionId         = "fulfilment-order-workflow";
    public const string FulfilmentOrderIdVar = "FulfilmentOrderId";

    protected override void Build(IWorkflowBuilder workflow)
    {
        workflow.DefinitionId = DefinitionId;
        workflow.Version      = 1;

        // FulfilmentOrderId is the only data carried in workflow state.
        workflow.WithVariable<string>(FulfilmentOrderIdVar);

        workflow.Root = new Sequence
        {
            Activities =
            [
                new CallOfaActivity(),   // OFA HTTP call — Level-1 resilience via Elsa.Resilience
                // TODO: ApplyAllocationActivity  — applies domain method + persists aggregate
                // TODO: SuspendFulfilmentActivity — bookmark awaiting next trigger
            ]
        };
    }
}
