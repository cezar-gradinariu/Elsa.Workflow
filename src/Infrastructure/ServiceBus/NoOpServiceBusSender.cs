using Elsa.Workflow.Application.Contracts.ServiceBus;
using Elsa.Workflow.Application.ServiceBus;
using Microsoft.Extensions.Logging;

namespace Elsa.Workflow.Infrastructure.ServiceBus;

/// <summary>
/// Development/stub implementation of <see cref="IServiceBusSender"/>.
/// Logs the outbound message instead of publishing to a real Service Bus.
///
/// Replace with an Azure.Messaging.ServiceBus implementation for production.
/// </summary>
public sealed class NoOpServiceBusSender(ILogger<NoOpServiceBusSender> logger) : IServiceBusSender
{
    public Task SendPrepareSubstoreAsync(PrepareSubstoreCommand command, CancellationToken ct = default)
    {
        logger.LogWarning(
            "[NoOp] PrepareSubstoreCommand not published — no Service Bus configured. " +
            "PrepareCommandId={PrepareCommandId} SubStoreId={SubStoreId} FulfilmentOrderId={FulfilmentOrderId} Lines={LineCount}",
            command.PrepareCommandId,
            command.SubStoreId,
            command.FulfilmentOrderId,
            command.Lines.Count);

        return Task.CompletedTask;
    }
}
