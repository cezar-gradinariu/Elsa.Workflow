namespace Elsa.Workflow.Domain.Exceptions;

public sealed class OptimisticConcurrencyException : DomainException
{
    public OptimisticConcurrencyException(string aggregateId)
        : base($"Concurrency conflict: aggregate '{aggregateId}' was modified by another process. Reload and retry.") { }
}
