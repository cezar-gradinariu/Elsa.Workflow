using System.Diagnostics;
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
///                      When a debugger is attached, also start Elsa Studio on a real
///                      Kestrel port so the UI can be browsed while breakpoints are active.
///   [AfterTestRun]   — stop and dispose the container (and Studio host if running).
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
///
/// Elsa Studio debug access:
///   When Debugger.IsAttached a StudioKestrelHost is started in BeforeTestRun
///   (shared across all scenarios). It uses a fixed database name so the Studio has
///   a stable view; per-scenario databases are still isolated in FulfilmentApiFactory.
///   The Studio URL is printed to the console once at startup.
/// </summary>
[Binding]
public sealed class Hooks(IObjectContainer objectContainer)
{
    private static MongoDbContainer _mongo = null!;

    // Studio host is run-scoped (one per test run) when a debugger is attached.
    private static StudioProcess? _studioFactory;

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

        if (Debugger.IsAttached)
            StartStudioHost();
    }

    [AfterTestRun]
    public static async Task StopMongoContainerAsync()
    {
        _studioFactory?.Dispose();
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

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static void StartStudioHost()
    {
        // Studio uses a shared "debug-studio" database — separate from per-scenario
        // databases — so the Studio session stays stable across scenario boundaries.
        const string studioDb = "debug-studio";

        _studioFactory = new StudioProcess(_mongo.GetConnectionString(), studioDb);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine();
        Console.WriteLine($"  ╔══════════════════════════════════════════════════╗");
        Console.WriteLine($"  ║  ELSA STUDIO  {_studioFactory.StudioUrl,-35}║");
        Console.WriteLine($"  ║  ELSA API     {_studioFactory.StudioUrl}/elsa/api   ║");
        Console.WriteLine($"  ╚══════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.ResetColor();
    }
}
