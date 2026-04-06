using Elsa.Extensions;
using Elsa.Features.Services;
using Elsa.Persistence.MongoDb.Extensions;
using Elsa.Persistence.MongoDb.Modules.Management;
using Elsa.Persistence.MongoDb.Modules.Runtime;
using MongoDB.Driver;

namespace Elsa.Workflow.Infrastructure.Extensions;

public static class ElsaInfrastructureExtensions
{
    /// <summary>
    /// Configures Elsa to use MongoDB for workflow management and runtime state.
    /// Keeps Elsa.Persistence.MongoDb references contained within the Infrastructure layer.
    ///
    /// The database name is embedded into the connection string via MongoUrlBuilder before
    /// being passed to Elsa. Elsa's MongoDbFeature reads the database name exclusively from
    /// MongoUrl.DatabaseName (not from opts.DatabaseName), so the URL must carry it.
    /// </summary>
    public static IModule UseElsaMongoDb(
        this IModule elsa,
        string connectionString,
        string databaseName)
    {
        // Embed the database name into the URL so Elsa's MongoDbFeature.CreateDatabase
        // resolves it from MongoUrl.DatabaseName rather than getting a null.
        var urlBuilder = new MongoUrlBuilder(connectionString)
        {
            DatabaseName = databaseName
        };
        var connectionStringWithDb = urlBuilder.ToMongoUrl().ToString();

        elsa.UseMongoDb(connectionStringWithDb, opts => opts.DatabaseName = databaseName);
        elsa.UseWorkflowManagement(mgmt => mgmt.UseMongoDb(_ => { }));
        elsa.UseWorkflowRuntime(rt => rt.UseMongoDb(_ => { }));
        return elsa;
    }
}
