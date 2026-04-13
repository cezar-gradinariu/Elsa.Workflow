using Elsa.Workflow.Api.Models;
using Elsa.Workflow.Application.Commands;
using Microsoft.AspNetCore.Mvc;

namespace Elsa.Workflow.Api.Controllers;

[ApiController]
[Route("api/prepare-reports")]
[Produces("application/json")]
public sealed class PrepareReportsController(
    ICommandHandler<RegisterPrepareReportCommand> handler)
    : ControllerBase
{
    /// <summary>
    /// Delivers a PrepareReport from a substore back to the workflow.
    ///
    /// The report is correlated to the original Prepare command via <c>prepareCommandId</c>.
    /// Multiple reports for the same ID are accepted — the last one wins (see
    /// FulfilmentOrder.RegisterContainers).  The workflow bookmark is re-registered after
    /// each call so further updates are always accepted.
    /// </summary>
    /// <response code="200">Report accepted and workflow resumed.</response>
    /// <response code="400">Request payload is invalid.</response>
    /// <response code="404">No active bookmark found for the given prepareCommandId.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Post(
        [FromBody] RegisterPrepareReportRequest request,
        CancellationToken ct)
    {
        var command = new RegisterPrepareReportCommand(
            PrepareCommandId: request.PrepareCommandId,
            Containers: request.Containers
                .Select(c => new ReportContainerInput(
                    c.ContainerId,
                    c.Lines.Select(l => new ReportContainerLineInput(l.OrderLineNo, l.Quantity))
                           .ToList()
                           .AsReadOnly()))
                .ToList()
                .AsReadOnly());

        await handler.HandleAsync(command, ct);
        return Ok();
    }
}
