using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
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
/// Elsa Studio debug access:
///   When a debugger is attached, Hooks.cs creates a separate <see cref="StudioKestrelHost"/>
///   that runs the full app stack on a real Kestrel port, giving browsers direct access
///   to Elsa Studio. This factory stays on TestServer so test HTTP clients work normally.
/// </summary>
public sealed class FulfilmentApiFactory(string mongoConnectionString, string databaseName)
    : WebApplicationFactory<Program>
{
    // Elsa's ConfigureMongoDbSerializers hosted service registers process-wide BSON
    // serializers. It must run exactly once per process (the first factory), then be
    // suppressed for all subsequent factories to prevent duplicate-registration throws.
    // StudioKestrelHost also participates in this protocol.
    internal static volatile bool BsonSerializersInitialized;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Use "Testing" environment instead of "Development" so that ASP.NET Core
        // does NOT set ValidateOnBuild = true. Blazor circuit-scoped services
        // (IBlazorServiceAccessor, ILocalizer) registered by Elsa Studio can only
        // be constructed inside an active SignalR circuit; DI validation would fail
        // for every service that depends on them.
        builder.UseEnvironment("Testing");

        // Build the per-scenario connection string with the database name embedded.
        // MongoUrlBuilder correctly handles credentials and query params already in
        // the base Testcontainers URL (e.g. mongodb://mongo:mongo@host:port/?directConnection=true).
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
            // We let the first host's service run, then remove it from all subsequent ones.
            if (BsonSerializersInitialized)
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
                BsonSerializersInitialized = true;
            }
        });
    }
}

/// <summary>
/// Launches the Api as a separate OS process so Elsa Studio is accessible in a
/// browser during debugging without being frozen by debugger breakpoints.
///
/// Running in-process (StudioKestrelHost) does not work for interactive debugging:
/// when a breakpoint fires the .NET debugger suspends ALL managed threads in the
/// process — including Kestrel's IO thread pool — so every browser request hangs.
/// A child process has its own thread pool that is entirely unaffected by the
/// parent's breakpoints.
///
/// The Api binary is already compiled into the test output directory because the
/// test project has a ProjectReference to it. We launch it with <c>dotnet exec</c>
/// and inject the Testcontainers MongoDB URL + Studio backend URL via environment
/// variables so the child process reads them as highest-priority configuration.
///
/// Lifecycle: call Start() once per test run; Dispose() kills the child process.
/// </summary>
public sealed class StudioProcess : IDisposable
{
    private readonly Process      _process;
    private readonly StreamWriter _logWriter;

    public string StudioUrl { get; }

    public StudioProcess(string mongoConnectionString, string databaseName)
    {
        var port  = AllocateFreePort();
        StudioUrl = $"http://localhost:{port}";

        var urlBuilder = new MongoUrlBuilder(mongoConnectionString)
        {
            DatabaseName         = databaseName,
            AuthenticationSource = "admin"
        };
        var connectionStringWithDb = urlBuilder.ToMongoUrl().ToString();

        // The Api DLL is in the same directory as the test binary because the test
        // project holds a ProjectReference to the Api project.
        var apiDll = Path.Combine(AppContext.BaseDirectory, "Elsa.Workflow.Api.dll");

        var psi = new ProcessStartInfo("dotnet", $"exec \"{apiDll}\"")
        {
            UseShellExecute  = false,
            CreateNoWindow   = true,
            // ASP.NET Core uses '__' as the hierarchy separator for env-var config.
            Environment =
            {
                ["ASPNETCORE_ENVIRONMENT"]   = "Development",
                ["ASPNETCORE_URLS"]          = $"http://127.0.0.1:{port}",
                ["MongoDB__ConnectionString"] = connectionStringWithDb,
                ["MongoDB__DatabaseName"]    = databaseName,
                ["ElsaApi__BaseUrl"]         = $"http://127.0.0.1:{port}/elsa/api",
            }
        };

        var logFile = Path.Combine(Path.GetTempPath(), $"studio-{port}.log");
        _logWriter                 = new StreamWriter(logFile, append: false) { AutoFlush = true };
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError  = true;

        _process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start Studio process.");

        _process.OutputDataReceived += (_, e) => { if (e.Data != null) _logWriter.WriteLine(e.Data); };
        _process.ErrorDataReceived  += (_, e) => { if (e.Data != null) _logWriter.WriteLine("[ERR] " + e.Data); };
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        Console.WriteLine($"[Studio] log → {logFile}");

        WaitUntilReady(port);
    }

    public void Dispose()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(5_000);
        }
        _process.Dispose();
        _logWriter.Dispose();
    }

    /// <summary>
    /// Polls the health endpoint until the process is ready or times out.
    /// </summary>
    private static void WaitUntilReady(int port, int timeoutMs = 30_000)
    {
        using var http    = new HttpClient();
        var deadline      = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var url           = $"http://127.0.0.1:{port}/elsa/api/workflow-definitions?page=0&pageSize=1";

        while (DateTime.UtcNow < deadline)
        {
            Thread.Sleep(500);
            try
            {
                var response = http.GetAsync(url).GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return;
            }
            catch { /* not ready yet */ }
        }
    }

    private static int AllocateFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
