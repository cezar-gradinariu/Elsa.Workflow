using Elsa.Workflow.Api.Models;
using Elsa.Workflow.Application.Commands;
using Elsa.Workflow.Application.Queries;
using Elsa.Workflow.Domain.Enums;
using Elsa.Workflow.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace Elsa.Workflow.Api.Controllers;

[ApiController]
[Route("api/fulfilments")]
[Produces("application/json")]
public sealed class FulfilmentsController(
    ICommandHandler<CreateFulfilmentOrderCommand>  createHandler,
    ICommandHandler<DeleteFulfilmentOrderCommand>  deleteHandler,
    ICommandHandler<ApproveFulfilmentOrderCommand> approveHandler,
    IQueryHandler<GetFulfilmentOrderQuery, GetFulfilmentOrderResult> getHandler)
    : ControllerBase
{
    /// <summary>
    /// Creates a new fulfilment order and starts its processing workflow.
    /// </summary>
    /// <response code="200">Order created and workflow started.</response>
    /// <response code="400">Request payload is invalid.</response>
    /// <response code="409">A fulfilment order with this ID already exists.</response>
    /// <response code="422">Domain rule violation (e.g. duplicate line numbers, negative quantities).</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateFulfilmentOrderRequest request,
        CancellationToken ct)
    {
        var command = new CreateFulfilmentOrderCommand(
            FulfilmentOrderId: FulfilmentOrderId.From(request.FulfilmentOrderId),
            StoreId:           StoreId.From(request.StoreId),
            OrderId:           OrderId.From(request.OrderId),
            OrderLines:        request.OrderLines
                .Select(l => new OrderLineInput(
                    l.OrderLineNo,
                    l.ArticleId,
                    l.ExpectedQuantity,
                    Enum.Parse<UnitOfMeasure>(l.UnitOfMeasure, ignoreCase: true),
                    l.CustomerSupplyInstructions))
                .ToList()
                .AsReadOnly());

        await createHandler.HandleAsync(command, ct);
        return Ok();
    }

    /// <summary>
    /// Returns the current state of a fulfilment order.
    /// </summary>
    /// <response code="200">Fulfilment order found.</response>
    /// <response code="404">Fulfilment order not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FulfilmentOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await getHandler.HandleAsync(
            new GetFulfilmentOrderQuery(FulfilmentOrderId.From(id)), ct);

        return Ok(FulfilmentOrderResponse.From(result.Order));
    }

    /// <summary>
    /// Approves or rejects a fulfilment order, signalling the waiting workflow.
    /// Approved = true  → logs success and completes the workflow.
    /// Approved = false → logs rejection and terminates the workflow.
    /// </summary>
    /// <response code="204">Signal delivered successfully.</response>
    /// <response code="404">No workflow is waiting for approval for this fulfilment order.</response>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] bool approved, CancellationToken ct)
    {
        await approveHandler.HandleAsync(
            new ApproveFulfilmentOrderCommand(FulfilmentOrderId.From(id), approved), ct);

        return NoContent();
    }

    /// <summary>
    /// Cancels and removes a fulfilment order and its associated workflow.
    /// </summary>
    /// <response code="204">Deleted successfully.</response>
    /// <response code="404">Fulfilment order not found.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await deleteHandler.HandleAsync(
            new DeleteFulfilmentOrderCommand(FulfilmentOrderId.From(id)), ct);

        return NoContent();
    }
}
