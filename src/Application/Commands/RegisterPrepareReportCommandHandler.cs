using Elsa.Workflow.Application.Activities;
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Options;

namespace Elsa.Workflow.Application.Commands;

/// <summary>
/// Wakes up the ManagePrepareActivity bookmark waiting for a specific PrepareReport.
///
/// Uses IWorkflowResumer.ResumeAsync&lt;TActivity&gt; — Elsa hashes the stimulus internally,
/// finds the matching bookmark on ManagePrepareActivity, and resumes the workflow instance.
///
/// The report DTO is passed via ResumeBookmarkOptions.Input["PrepareReport"].
/// ManagePrepareActivity.OnPrepareReportReceived reads it from WorkflowExecutionContext.Input.
/// </summary>
public sealed class RegisterPrepareReportCommandHandler(
    IWorkflowResumer workflowResumer)
    : ICommandHandler<RegisterPrepareReportCommand>
{
    public async Task HandleAsync(RegisterPrepareReportCommand command, CancellationToken ct = default)
    {
        var report = new PrepareReportDto(
            PrepareCommandId: command.PrepareCommandId,
            Containers: command.Containers
                .Select(c => new PrepareContainerDto(
                    c.ContainerId,
                    c.Lines.Select(l => new PrepareContainerLineDto(l.OrderLineNo, l.Quantity))
                           .ToList()
                           .AsReadOnly()))
                .ToList()
                .AsReadOnly());

        // T = ManagePrepareActivity (the activity type that owns the bookmark).
        // The stimulus (PrepareBookmarkPayload) is hashed by Elsa using the activity's
        // registered type name, which matches what ManagePrepareActivity set when it
        // called CreateBookmark with IncludeActivityInstanceId = false.
        var responses = (await workflowResumer.ResumeAsync<ManagePrepareActivity>(
            new PrepareBookmarkPayload(command.PrepareCommandId),
            new ResumeBookmarkOptions
            {
                Input = new Dictionary<string, object>
                {
                    ["PrepareReport"] = report
                }
            },
            ct)).ToList();

        if (responses.Count == 0)
            throw new InvalidOperationException(
                $"No active bookmark found for PrepareCommandId '{command.PrepareCommandId}'. " +
                "The workflow may not have reached the Prepare phase yet, or the ID is incorrect.");
    }
}
