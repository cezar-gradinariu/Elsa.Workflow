using Elsa.Workflow.Domain.Exceptions;
using Elsa.Workflow.Domain.Repositories;

namespace Elsa.Workflow.Application.Queries;

public sealed class GetFulfilmentOrderQueryHandler(IFulfilmentOrderRepository repository)
    : IQueryHandler<GetFulfilmentOrderQuery, GetFulfilmentOrderResult>
{
    public async Task<GetFulfilmentOrderResult> HandleAsync(
        GetFulfilmentOrderQuery query, CancellationToken ct = default)
    {
        var order = await repository.FindByIdAsync(query.FulfilmentOrderId, ct);

        if (order is null)
            throw new FulfilmentOrderNotFoundException(query.FulfilmentOrderId);

        return new GetFulfilmentOrderResult(order);
    }
}
