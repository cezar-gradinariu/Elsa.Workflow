using System.Net.Http.Json;
using Elsa.Extensions;
using Elsa.Resilience;
using Elsa.Resilience.Extensions;
using Elsa.Resilience.Models;
using Elsa.Workflows;
using Elsa.Workflow.Application.Contracts.Ofa;
using Elsa.Workflow.Application.Resilience;
using Elsa.Workflow.Application.Workflows;
using Elsa.Workflow.Domain.Aggregates;
using Elsa.Workflow.Domain.Repositories;
using Elsa.Workflow.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace Elsa.Workflow.Application.Activities;

/// <summary>
/// First substantive step in <see cref="FulfilmentOrderWorkflow"/>.
///
/// Loads the <see cref="FulfilmentOrder"/> aggregate, calls the OFA HTTP API,
/// maps the response to <see cref="OrderAllocationResult"/>, applies it to the
/// aggregate, and persists the updated aggregate.
///
/// Resilience (ADR-004 / ADR-006):
///   Level 1 — Polly retry + circuit breaker via <see cref="OfaResilienceStrategy"/>,
///   executed transparently by <see cref="IResilientActivityInvoker"/>.
///   Level 2 — if Level 1 exhausts, the exception propagates; Elsa records the
///   workflow as Faulted. An operator can resume via the Alterations API (OQ-3).
///
/// Data boundary (ADR-010):
///   The aggregate is loaded from the repository — never from workflow variables.
///   Only <c>FulfilmentOrderId</c> travels in workflow state.
/// </summary>
public class CallOfaActivity : Activity, IResilientActivity
{

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        // Wire the Polly pipeline for this execution. Done here (not in the constructor)
        // because CustomProperties values are serialised to MongoDB as part of the workflow
        // definition; JsonObject does not survive the round-trip (comes back as JsonElement,
        // which the evaluator wraps as JsonValue<JsonElement> — AsObject() then throws
        // NodeWrongType). Setting the strategy on the execution context avoids the
        // serialisation path entirely.
        context.SetResilienceStrategy(new ResilienceStrategyConfig
        {
            Mode       = ResilienceStrategyConfigMode.Identifier,
            StrategyId = OfaResilienceStrategy.StrategyId,
        }.SerializeToNode());

        var sp         = context.WorkflowExecutionContext.ServiceProvider;
        var repository = sp.GetRequiredService<IFulfilmentOrderRepository>();
        var invoker    = sp.GetRequiredService<IResilientActivityInvoker>();
        var httpFactory = sp.GetRequiredService<IHttpClientFactory>();

        var fulfilmentOrderId = context.GetVariable<string>(FulfilmentOrderWorkflow.FulfilmentOrderIdVar)
            ?? throw new InvalidOperationException(
                $"Workflow variable '{FulfilmentOrderWorkflow.FulfilmentOrderIdVar}' is missing.");

        var order = await repository.FindByIdAsync(
            FulfilmentOrderId.From(Guid.Parse(fulfilmentOrderId)),
            context.CancellationToken)
            ?? throw new InvalidOperationException(
                $"FulfilmentOrder '{fulfilmentOrderId}' not found.");

        // Level 1: IResilientActivityInvoker wraps the action in the Polly pipeline
        // configured by OfaResilienceStrategy (retry × 3 + circuit breaker).
        var response = await invoker.InvokeAsync<OfaAllocationResponse>(
            activity:          this,
            context:           context,
            action:            async () =>
            {
                var client  = httpFactory.CreateClient("OFA");
                var payload = BuildRequest(order);
                var http    = await client.PostAsJsonAsync("/allocate", payload, context.CancellationToken);
                http.EnsureSuccessStatusCode();
                return (await http.Content.ReadFromJsonAsync<OfaAllocationResponse>(context.CancellationToken))!;
            },
            cancellationToken: context.CancellationToken);

        var allocationResult = MapToDomain(response);
        order.ApplyAllocation(allocationResult);

        await repository.UpdateAsync(order, context.CancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Returns contextual details recorded against each Polly retry attempt.
    /// Visible in the Elsa Studio retry history panel and via
    /// GET /elsa/api/resilience/retries/{activityInstanceId}.
    /// </remarks>
    public IDictionary<string, string?> CollectRetryDetails(
        ActivityExecutionContext context,
        RetryAttempt             attempt)
    {
        var orderId = context.GetVariable<string>(FulfilmentOrderWorkflow.FulfilmentOrderIdVar);

        return new Dictionary<string, string?>
        {
            ["fulfilmentOrderId"] = orderId,
            ["attemptNumber"]     = attempt.AttemptNumber.ToString(),
            ["retryDelay"]        = attempt.RetryDelay.ToString(),
            ["exception"]         = attempt.Exception?.GetType().Name,
            ["exceptionMessage"]  = attempt.Exception?.Message,
        };
    }

    // ── Mapping helpers ────────────────────────���───────────────────────────────

    private static OfaAllocationRequest BuildRequest(FulfilmentOrder order) =>
        new(
            OrderId:    order.OrderId.Value,
            StoreId:    order.StoreId.Value,
            OrderLines: order.OrderLines
                .Select(l => new OfaOrderLineRequest(
                    OrderLineNo:      l.OrderLineNo,
                    ArticleId:        l.ArticleId,
                    ExpectedQuantity: l.ExpectedQuantity,
                    UnitOfMeasure:    l.UnitOfMeasure.ToString()))
                .ToList()
        );

    private static Domain.ValueObjects.OrderAllocationResult MapToDomain(OfaAllocationResponse response)
    {
        // Group sub-store lines by order line number to build LineAllocation objects.
        var lineMap = response.SubStoreAllocations
            .SelectMany(sub => sub.Lines.Select(line => (SubStoreId: sub.SubStoreId, Line: line)))
            .GroupBy(x => x.Line.OrderLineNo)
            .Select(g => new Domain.ValueObjects.LineAllocation(
                OrderLineNo:         g.Key,
                SubStoreAllocations: g.Select(x =>
                    new Domain.ValueObjects.SubStoreAllocation(
                        SubStoreId:        x.SubStoreId,
                        AllocatedQuantity: x.Line.AllocatedQuantity))
                    .ToList()))
            .ToList();

        return new Domain.ValueObjects.OrderAllocationResult(lineMap);
    }
}
