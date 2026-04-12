using Elsa.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Elsa.Workflow.Application.Resilience;

/// <summary>
/// Polly pipeline for calls to the Order Fulfilment Allocator (OFA) HTTP API.
///
/// Level 1 fast resilience (ADR-004 / ADR-006):
///   - Retry: 3 attempts, exponential back-off starting at 1 s, with jitter.
///   - Circuit breaker: opens when ≥ 50 % of calls fail over a 30 s sampling
///     window (min 5 calls); stays open for 15 s before probing again.
///
/// Registered via <see cref="ApplicationResilienceStrategySource"/> so the Elsa
/// Studio can discover and display it as a named strategy.
/// </summary>
public class OfaResilienceStrategy : IResilienceStrategy
{
    public const string StrategyId = "ofa-resilience";

    public string Id          { get; set; } = StrategyId;
    public string DisplayName { get; set; } = "OFA Retry + Circuit Breaker";

    public Task ConfigurePipeline<T>(ResiliencePipelineBuilder<T> pipelineBuilder, ResilienceContext context)
    {
        pipelineBuilder
            .AddRetry(new RetryStrategyOptions<T>
            {
                ShouldHandle    = args => new ValueTask<bool>(IsTransient(args.Outcome.Exception)),
                MaxRetryAttempts = 3,
                Delay            = TimeSpan.FromSeconds(1),
                BackoffType      = DelayBackoffType.Exponential,
                UseJitter        = true,
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<T>
            {
                ShouldHandle       = args => new ValueTask<bool>(IsTransient(args.Outcome.Exception)),
                SamplingDuration   = TimeSpan.FromSeconds(30),
                FailureRatio       = 0.5,
                MinimumThroughput  = 5,
                BreakDuration      = TimeSpan.FromSeconds(15),
            });

        return Task.CompletedTask;
    }

    // Mirrors the types recognised by Elsa's DefaultTransientExceptionStrategy.
    private static bool IsTransient(Exception? ex) =>
        ex is HttpRequestException or TaskCanceledException or TimeoutException;
}
