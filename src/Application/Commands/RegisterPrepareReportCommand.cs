namespace Elsa.Workflow.Application.Commands;

public sealed record RegisterPrepareReportCommand(
    string PrepareCommandId,
    IReadOnlyList<ReportContainerInput> Containers);

public sealed record ReportContainerInput(
    string ContainerId,
    IReadOnlyList<ReportContainerLineInput> Lines);

public sealed record ReportContainerLineInput(int OrderLineNo, decimal Quantity);
