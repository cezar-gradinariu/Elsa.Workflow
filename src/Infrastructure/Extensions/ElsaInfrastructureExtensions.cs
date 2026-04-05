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
}
