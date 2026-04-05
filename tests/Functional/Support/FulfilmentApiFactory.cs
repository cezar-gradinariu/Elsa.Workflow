using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;

namespace Elsa.Workflow.Functional.Tests.Support;

/// <summary>
/// WebApplicationFactory wired to a real MongoDB instance provided by Testcontainers.
/// The test server runs in-process via TestServer (no real TCP socket).
///
/// Config injection strategy:
///   Program.cs reads builder.Configuration["MongoDB:ConnectionString"] early (before
///   ConfigureAppConfiguration sources are appended by the factory). We therefore
///   replace the IMongoClient and IMongoDatabase registrations explicitly in
///   ConfigureServices, which runs AFTER Program.cs has registered all services but
///   BEFORE the DI container is sealed. This guarantees the Testcontainers-supplied
///   connection string is used for every MongoDB operation in the test host.
///
/// Connection string note:
///   Elsa's MongoDbFeature reads the database name from MongoUrl.DatabaseName parsed
///   from the connection string. We use MongoUrlBuilder to inject the database name
///   into the base Testcontainers URL, preserving credentials and query params
///   (e.g. ?directConnection=true) that the container requires.
///
/// BSON serializer conflict:
///   Elsa's ConfigureMongoDbSerializers hosted service registers process-wide BSON
///   serializers. Running multiple factories in the same process throws on the second
///   registration. We remove that service so only the first registration persists —
///   it remains correct for all subsequent scenarios.
///
/// ELSA UI EXTENSION POINT:
///   When you are ready to add Elsa Studio, two changes are needed here:
///
///   1. Bind a real Kestrel port so a browser can connect:
///      Override CreateHost(IHostBuilder) and build a second real-listening host in
///      parallel alongside the TestServer host (the "real port" WAF pattern).
///
///   2. Register Elsa Studio inside ConfigureServices:
///          services.AddElsaStudio(opts => opts.ServerUrl = "/elsa/api");
///      And in Program.cs, add behind an IsDevelopment() guard:
///          app.MapElsaStudio();
/// </summary>
public sealed class FulfilmentApiFactory(string mongoConnectionString, string databaseName)
    : WebApplicationFactory<Program>
{
    // Elsa's ConfigureMongoDbSerializers hosted service registers process-wide BSON
    // serializers. It must run exactly once per process (the first factory), then be
    // suppressed for all subsequent factories to prevent duplicate-registration throws.
    private static volatile bool _bsonSerializersInitialized;
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Build the per-scenario connection string with the database name embedded.
        // MongoUrlBuilder correctly handles credentials and query params already in
        // the base Testcontainers URL (e.g. mongodb://mongo:mongo@host:port/?directConnection=true).
        // Inject the database name into the Testcontainers URL.
        // AuthenticationSource must remain "admin" — that's where the container's root
        // user lives. Without it, MongoDB tries to auth against the database path which
        // doesn't exist yet, causing SCRAM-SHA-1 authentication failures.
        var urlBuilder = new MongoUrlBuilder(mongoConnectionString)
        {
            DatabaseName         = databaseName,
            AuthenticationSource = "admin"
        };
        var connectionStringWithDb = urlBuilder.ToMongoUrl().ToString();

        builder.ConfigureServices(services =>
        {
            // --- Replace IMongoClient ---
            // Remove ALL IMongoClient registrations (Program.cs + Elsa may both register one).
            // Replace with a single client built from the full Testcontainers URL so every
            // MongoDB operation goes through the same authenticated connection.
            var clientDescriptors = services.Where(d => d.ServiceType == typeof(IMongoClient)).ToList();
            foreach (var d in clientDescriptors) services.Remove(d);

            services.AddSingleton<IMongoClient>(_ => new MongoClient(connectionStringWithDb));

            // --- Replace IMongoDatabase ---
            // Remove Program.cs's scoped IMongoDatabase and re-add one that reads from
            // our replacement IMongoClient with the correct database name.
            var dbDescriptors = services.Where(d => d.ServiceType == typeof(IMongoDatabase)).ToList();
            foreach (var d in dbDescriptors) services.Remove(d);

            services.AddScoped<IMongoDatabase>(sp =>
                sp.GetRequiredService<IMongoClient>().GetDatabase(databaseName));

            // --- Elsa BSON serializer hosted service (run once, suppress thereafter) ---
            // The service registers process-wide serializers; a second registration throws.
            // We let the first factory's service run, then remove it from all subsequent ones.
            if (_bsonSerializersInitialized)
            {
                var elsaHostedServices = services
                    .Where(d => d.ServiceType == typeof(IHostedService)
                             && d.ImplementationType?.Namespace?
                                 .StartsWith("Elsa.Persistence.MongoDb") == true)
                    .ToList();
                foreach (var svc in elsaHostedServices)
                    services.Remove(svc);
            }
            else
            {
                _bsonSerializersInitialized = true;
            }
        });
    }
}
