using DotNet.Testcontainers.Builders;
using MongoDB.Driver;
using Reqnroll;
using Reqnroll.BoDi;
using Testcontainers.MongoDb;

namespace Elsa.Workflow.Functional.Tests.Support;

/// <summary>
/// Reqnroll lifecycle hooks.
///
/// Container lifecycle:
///   [BeforeTestRun]  — start a single MongoDB container shared by all scenarios.
///   [AfterTestRun]   — stop and dispose the container.
///
/// Factory lifecycle:
///   [BeforeScenario] — create a per-scenario WebApplicationFactory pointing at its
///                      own database name (guarantees complete isolation).
///   [AfterScenario]  — dispose the factory and clear the test env vars.
///
/// Config injection strategy:
///   Program.cs reads builder.Configuration["MongoDB:*"] BEFORE builder.Build()
///   (as local variables captured by the AddElsa lambda). At that point,
///   WebApplicationFactory's ConfigureAppConfiguration hasn't run yet — only the
///   initial configuration sources (appsettings.json, environment variables) are
///   available. We therefore inject the per-scenario connection string and database
///   name via environment variables (higher priority than appsettings.json) BEFORE
///   calling factory.CreateClient(), which is when the host is built and the
///   configuration is first read.
///
///   Scenarios run sequentially (Reqnroll default), so environment variable mutation
///   is safe. The vars are cleared in AfterScenario to avoid leaking into the OS.
/// </summary>
[Binding]
public sealed class Hooks(IObjectContainer objectContainer)
{
    private static MongoDbContainer _mongo = null!;

    // -------------------------------------------------------------------------
    // Test-run scope
    // -------------------------------------------------------------------------

    [BeforeTestRun]
    public static async Task StartMongoContainerAsync()
    {
        _mongo = new MongoDbBuilder()
            .WithImage("mongo:7.0")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(27017))
            .Build();

        await _mongo.StartAsync();
    }

    [AfterTestRun]
    public static async Task StopMongoContainerAsync()
    {
        if (_mongo is not null) await _mongo.DisposeAsync();
    }

    // -------------------------------------------------------------------------
    // Scenario scope
    // -------------------------------------------------------------------------

    [BeforeScenario]
    public void StartScenarioFactory()
    {
        // Unique database per scenario — full data isolation, no cross-scenario noise.
        var dbName = $"ft-{Guid.NewGuid():N}"[..24];

        // Use MongoUrlBuilder so the database name is inserted correctly even when
        // the base URL has credentials or query params (e.g. ?directConnection=true).
        // AuthenticationSource = "admin" so auth uses the container's root user database,
        // not the per-scenario database (which is empty and has no users).
        var urlBuilder = new MongoUrlBuilder(_mongo.GetConnectionString())
        {
            DatabaseName         = dbName,
            AuthenticationSource = "admin"
        };
        var connectionString = urlBuilder.ToMongoUrl().ToString();

        // ASP.NET Core env-var config uses '__' as hierarchy separator.
        // These are visible to builder.Configuration in Program.cs before Build()
        // because ConfigurationManager reads env vars as an initial source.
        Environment.SetEnvironmentVariable("MongoDB__ConnectionString", connectionString);
        Environment.SetEnvironmentVariable("MongoDB__DatabaseName", dbName);

        var factory = new FulfilmentApiFactory(connectionString, dbName);

        // CreateClient() triggers the one-time host build. Env vars are in place.
        objectContainer.RegisterInstanceAs<IDisposable>(factory, "factory");
        objectContainer.RegisterInstanceAs(factory.CreateClient());
        objectContainer.RegisterInstanceAs(new ScenarioState());
    }

    [AfterScenario]
    public void DisposeScenarioFactory()
    {
        if (objectContainer.IsRegistered<IDisposable>("factory"))
            objectContainer.Resolve<IDisposable>("factory").Dispose();

        // Clean up so the OS environment doesn't carry test values.
        Environment.SetEnvironmentVariable("MongoDB__ConnectionString", null);
        Environment.SetEnvironmentVariable("MongoDB__DatabaseName", null);
    }
}
