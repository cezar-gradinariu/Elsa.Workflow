namespace Elsa.Workflow.Api.Models;

public sealed record RegisterPrepareReportRequest(
    string PrepareCommandId,
    IReadOnlyList<PrepareReportContainerRequest> Containers);

public sealed record PrepareReportContainerRequest(
    string ContainerId,
    IReadOnlyList<PrepareReportContainerLineRequest> Lines);

public sealed record PrepareReportContainerLineRequest(int OrderLineNo, decimal Quantity);
