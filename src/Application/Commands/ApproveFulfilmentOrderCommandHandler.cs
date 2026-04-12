using Elsa.Workflow.Application.Workflows.Activities;
using Elsa.Workflow.Domain.Exceptions;
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Filters;
using Elsa.Workflows.Runtime.Messages;

namespace Elsa.Workflow.Application.Commands;

public sealed class ApproveFulfilmentOrderCommandHandler(
    IBookmarkStore   bookmarkStore,
    IWorkflowRuntime workflowRuntime)
    : ICommandHandler<ApproveFulfilmentOrderCommand>
{
    public async Task HandleAsync(ApproveFulfilmentOrderCommand command, CancellationToken ct = default)
    {
        var correlationId = command.FulfilmentOrderId.ToString();

        // Find the WaitForApproval bookmark for this fulfilment order.
        var bookmarks = await bookmarkStore.FindManyAsync(
            new BookmarkFilter
            {
                CorrelationId = correlationId,
                Name          = WaitForApprovalActivity.BookmarkName
            },
            ct);

        var bookmark = bookmarks.FirstOrDefault()
            ?? throw new FulfilmentOrderNotFoundException(command.FulfilmentOrderId);

        // Resume the suspended workflow instance, passing Approved in the input dict.
        var client = await workflowRuntime.CreateClientAsync(bookmark.WorkflowInstanceId, ct);

        await client.RunInstanceAsync(
            new RunWorkflowInstanceRequest
            {
                BookmarkId = bookmark.Id,
                Input      = new Dictionary<string, object> { ["Approved"] = command.Approved }
            },
            ct);
    }
}
