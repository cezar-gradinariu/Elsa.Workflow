using Elsa.Workflow.Application.Contracts.ServiceBus;

namespace Elsa.Workflow.Application.ServiceBus;

/// <summary>
/// Publishes outbound messages to the Service Bus.
/// The concrete implementation lives in Infrastructure and is swapped in via DI.
/// </summary>
public interface IServiceBusSender
{
    Task SendPrepareSubstoreAsync(
        PrepareSubstoreCommand command,
        CancellationToken      ct = default);
}
