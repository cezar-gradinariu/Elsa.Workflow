using Elsa.Workflow.Domain.ValueObjects;

namespace Elsa.Workflow.Application.Commands;

public sealed record DeleteFulfilmentOrderCommand(FulfilmentOrderId FulfilmentOrderId);
