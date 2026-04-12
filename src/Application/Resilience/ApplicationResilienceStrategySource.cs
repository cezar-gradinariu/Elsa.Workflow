using Elsa.Resilience;

namespace Elsa.Workflow.Application.Resilience;

/// <summary>
/// Registers all application-defined resilience strategies with the Elsa
/// <see cref="IResilienceStrategyCatalog"/> so they appear in the Studio
/// strategy picker and can be referenced by activity <c>CustomProperties</c>.
/// </summary>
public class ApplicationResilienceStrategySource : IResilienceStrategySource
{
    private static readonly IReadOnlyList<IResilienceStrategy> Strategies =
    [
        new OfaResilienceStrategy(),
    ];

    public Task<IEnumerable<IResilienceStrategy>> GetStrategiesAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<IResilienceStrategy>>(Strategies);
}
