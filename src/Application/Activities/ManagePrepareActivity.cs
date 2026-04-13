using Elsa.Extensions;
using Elsa.Workflow.Application.Contracts.ServiceBus;
using Elsa.Workflow.Application.ServiceBus;
using Elsa.Workflow.Application.Workflows;
using Elsa.Workflow.Domain.Aggregates;
using Elsa.Workflow.Domain.Repositories;
using Elsa.Workflow.Domain.ValueObjects;
using Elsa.Workflows;
using Elsa.Workflows.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Elsa.Workflow.Application.Activities;

/// <summary>
/// Fan-out activity that orchestrates the Prepare phase of a FulfilmentOrder.
///
/// Lifecycle:
///   1. First execution (ExecuteAsync):
///      - Loads the allocated aggregate from the repository.
///      - For each distinct substore: generates a stable PrepareCommandId, publishes a
///        PrepareSubstoreCommand to Service Bus, and registers a bookmark keyed on that ID.
///      - Stores prepareCommandId → subStoreId in the "PrepareCommandMap" workflow variable
///        so callbacks can look up which substore a report belongs to.
///      - Activity suspends (N bookmarks, one per substore).
///
///   2. Each time a PrepareReport arrives (OnPrepareReportReceived callback):
///      - Receives the report via WorkflowExecutionContext.Input["PrepareReport"].
///      - Last-write-wins: calls order.RegisterContainers and persists the aggregate.
///      - Re-registers the same bookmark so the activity waits for the next update.
///      - Activity re-suspends — it never "completes".
///
/// Data boundary (ADR-010):
///   Only FulfilmentOrderId and the PrepareCommandMap travel in workflow state.
///   All aggregate data is loaded fresh from the repository on each execution.
/// </summary>
public class ManagePrepareActivity : Activity
{
    private const string PrepareCommandMapVar = "PrepareCommandMap";

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var sp         = context.WorkflowExecutionContext.ServiceProvider;
        var repository = sp.GetRequiredService<IFulfilmentOrderRepository>();
        var sender     = sp.GetRequiredService<IServiceBusSender>();
        var logger     = sp.GetRequiredService<ILogger<ManagePrepareActivity>>();

        var orderId = context.GetVariable<string>(FulfilmentOrderWorkflow.FulfilmentOrderIdVar)
            ?? throw new InvalidOperationException(
                $"Workflow variable '{FulfilmentOrderWorkflow.FulfilmentOrderIdVar}' is missing.");

        var order = await repository.FindByIdAsync(
            FulfilmentOrderId.From(Guid.Parse(orderId)),
            context.CancellationToken)
            ?? throw new InvalidOperationException($"FulfilmentOrder '{orderId}' not found.");

        // Build substore → allocated-lines mapping from the applied allocation.
        var subStoreGroups = order.OrderLines
            .SelectMany(line => line.Allocations
                .Select(alloc => (SubStoreId: alloc.SubStoreId, Line: line, Alloc: alloc)))
            .GroupBy(x => x.SubStoreId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => new PrepareLineDto(
                    x.Line.OrderLineNo,
                    x.Line.ArticleId,
                    x.Alloc.AllocatedQuantity)).ToList());

        // Generate stable IDs, publish messages, register one bookmark per substore.
        var prepareCommandMap = new Dictionary<string, string>(); // prepareCommandId → subStoreId

        foreach (var (subStoreId, lines) in subStoreGroups)
        {
            var prepareCommandId = Guid.NewGuid().ToString();
            prepareCommandMap[prepareCommandId] = subStoreId;

            var command = new PrepareSubstoreCommand(
                PrepareCommandId:  prepareCommandId,
                FulfilmentOrderId: orderId,
                SubStoreId:        subStoreId,
                Lines:             lines);

            await sender.SendPrepareSubstoreAsync(command, context.CancellationToken);
            logger.LogInformation(
                "Published PrepareSubstoreCommand {PrepareCommandId} for substore {SubStoreId} on order {OrderId}",
                prepareCommandId, subStoreId, orderId);

            // One bookmark per substore — keyed on the stable PrepareCommandId.
            // IncludeActivityInstanceId = false so the hash can be reproduced externally
            // by RegisterPrepareReportCommandHandler without knowing the activity instance ID.
            context.CreateBookmark(new CreateBookmarkArgs
            {
                Stimulus                = new PrepareBookmarkPayload(prepareCommandId),
                Callback                = OnPrepareReportReceived,
                AutoBurn                = true,  // consumed on fire; callback re-registers it
                AutoComplete            = false, // activity never completes; re-suspends after each callback
                IncludeActivityInstanceId = false
            });
        }

        context.SetVariable(PrepareCommandMapVar, prepareCommandMap);
    }

    private async ValueTask OnPrepareReportReceived(ActivityExecutionContext context)
    {
        var sp         = context.WorkflowExecutionContext.ServiceProvider;
        var repository = sp.GetRequiredService<IFulfilmentOrderRepository>();
        var logger     = sp.GetRequiredService<ILogger<ManagePrepareActivity>>();

        // Input is passed by RegisterPrepareReportCommandHandler via RunInstanceAsync.
        var rawInput = context.WorkflowExecutionContext.Input
            ?? throw new InvalidOperationException("WorkflowExecutionContext.Input is null in callback.");

        if (!rawInput.TryGetValue("PrepareReport", out var rawReport) || rawReport is null)
            throw new InvalidOperationException("'PrepareReport' key missing from workflow input.");

        var report = rawReport as PrepareReportDto
            ?? throw new InvalidOperationException(
                $"Expected PrepareReportDto but got {rawReport.GetType().Name}.");

        // Resolve the subStoreId for this command from the persisted map.
        var prepareCommandMap = context.GetVariable<Dictionary<string, string>>(PrepareCommandMapVar)
            ?? throw new InvalidOperationException($"Workflow variable '{PrepareCommandMapVar}' not set.");

        if (!prepareCommandMap.TryGetValue(report.PrepareCommandId, out var subStoreId))
            throw new InvalidOperationException(
                $"PrepareCommandId '{report.PrepareCommandId}' not found in PrepareCommandMap.");

        var orderId = context.GetVariable<string>(FulfilmentOrderWorkflow.FulfilmentOrderIdVar)!;

        var order = await repository.FindByIdAsync(
            FulfilmentOrderId.From(Guid.Parse(orderId)),
            context.CancellationToken)
            ?? throw new InvalidOperationException($"FulfilmentOrder '{orderId}' not found.");

        // Map DTO → domain value objects and apply (last-write-wins).
        var containers = report.Containers
            .Select(c => new Container(
                c.ContainerId,
                c.Lines.Select(l => new ContainerLine(l.OrderLineNo, l.Quantity))
                        .ToList()
                        .AsReadOnly()))
            .ToList()
            .AsReadOnly() as IReadOnlyList<Container>;

        order.RegisterContainers(report.PrepareCommandId, subStoreId, containers);
        await repository.UpdateAsync(order, context.CancellationToken);

        logger.LogInformation(
            "Registered containers for PrepareCommandId {PrepareCommandId} substore {SubStoreId} on order {OrderId}",
            report.PrepareCommandId, subStoreId, orderId);

        // Re-register the bookmark for this same prepare command so the activity
        // can receive further updated reports (last-write-wins; goes back to sleep).
        context.CreateBookmark(new CreateBookmarkArgs
        {
            Stimulus                  = new PrepareBookmarkPayload(report.PrepareCommandId),
            Callback                  = OnPrepareReportReceived,
            AutoBurn                  = true,
            AutoComplete              = false,
            IncludeActivityInstanceId = false
        });
    }
}

/// <summary>
/// DTO passed via <c>WorkflowExecutionContext.Input["PrepareReport"]</c> when
/// a workflow bookmark is resumed by <see cref="Commands.RegisterPrepareReportCommandHandler"/>.
/// </summary>
public sealed record PrepareReportDto(
    string PrepareCommandId,
    IReadOnlyList<PrepareContainerDto> Containers);

public sealed record PrepareContainerDto(
    string ContainerId,
    IReadOnlyList<PrepareContainerLineDto> Lines);

public sealed record PrepareContainerLineDto(int OrderLineNo, decimal Quantity);
