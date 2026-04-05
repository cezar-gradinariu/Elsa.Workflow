using Elsa.Workflow.Domain.Aggregates;
using Elsa.Workflow.Domain.ValueObjects;

namespace Elsa.Workflow.Application.Queries;

public sealed record GetFulfilmentOrderQuery(FulfilmentOrderId FulfilmentOrderId);

public sealed record GetFulfilmentOrderResult(FulfilmentOrder Order);
