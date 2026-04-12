using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;

namespace Elsa.Workflow.Application.Workflows.Activities;

/// <summary>
/// Suspends the workflow until an external approval signal arrives.
/// Outputs true (approved) or false (rejected) so the caller can branch.
/// The resume input key "Approved" is populated by ApproveFulfilmentOrderCommandHandler.
/// </summary>
[Activity(
    Namespace = "MyCustomActivities",
    DisplayName = "Wait for Approval",
    Description = "Suspends the workflow until an external approval/rejection signal is received.",
    Category = "Control")]
public sealed class WaitForApprovalActivity : Activity<bool>
{
    public const string BookmarkName = "WaitForApproval";

    protected override ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        context.CreateBookmark(new CreateBookmarkArgs
        {
            BookmarkName = BookmarkName,
            Callback     = ResumeAsync
        });
        return ValueTask.CompletedTask;
    }

    private ValueTask ResumeAsync(ActivityExecutionContext context)
    {
        var input   = context.WorkflowExecutionContext.Input;
        var approved = input?.TryGetValue("Approved", out var raw) == true && raw is true;
        context.Set(Result, approved);
        return ValueTask.CompletedTask;
    }
}
