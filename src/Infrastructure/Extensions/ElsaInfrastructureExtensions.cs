using Elsa.Extensions;
using Elsa.Features.Services;
using Elsa.Persistence.MongoDb.Extensions;
using Elsa.Persistence.MongoDb.Modules.Management;
using Elsa.Persistence.MongoDb.Modules.Runtime;

namespace Elsa.Workflow.Infrastructure.Extensions;

public static class ElsaInfrastructureExtensions
{
    /// <summary>
    /// Configures Elsa to use MongoDB for workflow management and runtime state.
    /// Keeps Elsa.Persistence.MongoDb references contained within the Infrastructure layer.
    /// </summary>
    /// <summary>
    /// Configures Elsa to use MongoDB for workflow management and runtime state.
    /// The database name must be part of the connection string (e.g. .../elsa_ddd_db?authSource=admin).
    /// </summary>
    public static IModule UseElsaMongoDb(this IModule elsa, string connectionString)
    {
        elsa.UseMongoDb(connectionString);
        elsa.UseWorkflowManagement(mgmt => mgmt.UseMongoDb(_ => { }));
        elsa.UseWorkflowRuntime(rt => rt.UseMongoDb(_ => { }));
        return elsa;
    }
}
