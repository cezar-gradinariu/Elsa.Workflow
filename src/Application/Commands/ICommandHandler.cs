namespace Elsa.Workflow.Application.Commands;

public interface ICommandHandler<TCommand>
{
    Task HandleAsync(TCommand command, CancellationToken ct = default);
}
