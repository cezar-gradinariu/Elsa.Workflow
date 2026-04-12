using Elsa.Extensions;
using Elsa.Features.Services;
using Elsa.Persistence.MongoDb.Extensions;
using Elsa.Persistence.MongoDb.Modules.Management;
using Elsa.Persistence.MongoDb.Modules.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Elsa.Workflow.Infrastructure.Extensions;

public static class ElsaInfrastructureExtensions
{
    /// <summary>
    /// Configures Elsa to use MongoDB for workflow management and runtime state.
    /// Keeps Elsa.Persistence.MongoDb references contained within the Infrastructure layer.
    /// </summary>
    public static IModule UseElsaMongoDb(
        this IModule elsa,
        string connectionString,
        string databaseName)
    {
        elsa.UseMongoDb(connectionString, opts => opts.DatabaseName = databaseName);
        elsa.UseWorkflowManagement(mgmt => mgmt.UseMongoDb(_ => { }));
        elsa.UseWorkflowRuntime(rt => rt.UseMongoDb(_ => { }));
        return elsa;
    }

    /// <summary>
    /// Registers the named HttpClient("OFA") used by CallOfaActivity.
    ///
    /// No resilience handler is attached here — retry and circuit-breaker
    /// are owned by OfaResilienceStrategy and executed by
    /// IResilientActivityInvoker at the Elsa activity level (ADR-004 / ADR-006).
    /// Only base address and a hard per-request timeout are set at the transport level.
    /// </summary>
    public static IServiceCollection AddOfaHttpClient(
        this IServiceCollection services,
        string baseUrl)
    {
        services.AddHttpClient("OFA", client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            // Hard ceiling: if Polly's per-attempt timeout fires first this
            // acts as the ultimate backstop for the entire request chain.
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}
