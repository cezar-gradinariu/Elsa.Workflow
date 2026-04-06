using Elsa.Extensions;
using Elsa.Studio.Contracts;
using Elsa.Studio.Core.BlazorServer.Extensions;
using Elsa.Studio.Dashboard.Extensions;
using Elsa.Studio.Extensions;
using Elsa.Studio.Models;
using Elsa.Studio.Shell.Extensions;
using Elsa.Studio.Workflows.Extensions;
using Elsa.Workflow.Api.Middleware;
using Elsa.Workflow.Application.Commands;
using Elsa.Workflow.Application.Queries;
using Elsa.Workflow.Application.Workflows;
using Elsa.Workflow.Domain.Repositories;
using Elsa.Workflow.Infrastructure.Extensions;
using Elsa.Workflow.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Text.Encodings.Web;

namespace Elsa.Workflow.Api;

/// <summary>
/// Centralises all service registrations and middleware pipeline configuration.
/// Called by Program.cs and, for debug-time Elsa Studio access, by the test project's
/// StudioKestrelHost — which builds a real Kestrel WebApplication without
/// WebApplicationFactory so it can serve a browser alongside the in-process TestServer.
/// </summary>
public static class ProgramStartup
{
    /// <summary>
    /// Registers all services. <paramref name="config"/> must already contain
    /// <c>MongoDB:ConnectionString</c>, <c>MongoDB:DatabaseName</c>, and optionally
    /// <c>ElsaApi:BaseUrl</c> before this is called.
    /// </summary>
    public static void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        var connectionString = config["MongoDB:ConnectionString"]!;
        var databaseName     = config["MongoDB:DatabaseName"]!;
        var elsaApiUrl       = config["ElsaApi:BaseUrl"] ?? "http://localhost:5158/elsa/api";

        // ─── MongoDB ─────────────────────────────────────────────────────────────
        services.AddSingleton<IMongoClient>(_ => new MongoClient(connectionString));
        services.AddScoped<IMongoDatabase>(sp =>
            sp.GetRequiredService<IMongoClient>().GetDatabase(databaseName));

        // ─── Domain / Application ────────────────────────────────────────────────
        services.AddScoped<IFulfilmentOrderRepository, FulfilmentOrderRepository>();
        services.AddScoped<
            ICommandHandler<CreateFulfilmentOrderCommand>,
            CreateFulfilmentOrderCommandHandler>();
        services.AddScoped<
            ICommandHandler<DeleteFulfilmentOrderCommand>,
            DeleteFulfilmentOrderCommandHandler>();
        services.AddScoped<
            IQueryHandler<GetFulfilmentOrderQuery, GetFulfilmentOrderResult>,
            GetFulfilmentOrderQueryHandler>();

        // ─── Elsa Workflows ──────────────────────────────────────────────────────
        services.AddElsa(elsa =>
        {
            // MongoDB persistence via Infrastructure (keeps Elsa.Persistence.MongoDb
            // out of the Api layer — ADR-001/003).
            elsa.UseElsaMongoDb(connectionString, databaseName);

            // Code-first workflow definitions from the Application assembly.
            elsa.AddWorkflowsFrom<FulfilmentOrderWorkflow>();

            // FastEndpoints + Elsa REST API endpoints (consumed by Elsa Studio).
            elsa.UseWorkflowsApi();
        });

        // ─── Elsa Studio (Blazor Server) ─────────────────────────────────────────
        // Classic Blazor Server pattern: AddRazorPages + AddServerSideBlazor + _Host.cshtml.
        // AddCore() registers IBlazorServiceAccessor and ILocalizer (circuit-scoped)
        // which Elsa Studio modules depend on. Must come before AddShell/AddDashboard/etc.
        services.AddRazorPages();
        services.AddServerSideBlazor();
        services.AddCore();
        // IAuthenticationProviderManager is required by Elsa Studio's workflow
        // services but is only registered by the auth modules (ElsaIdentity, OIDC).
        // Since authorization is disabled, register a no-op so DI validation passes.
        services.AddScoped<IAuthenticationProviderManager, NoOpAuthenticationProviderManager>();
        services
            .AddShell(opts => opts.DisableAuthorization = true)
            .AddRemoteBackend(new BackendApiConfig
            {
                ConfigureBackendOptions = opts => opts.Url = new Uri(elsaApiUrl)
            })
            .AddDashboardModule()
            .AddWorkflowsModule();

        // ─── Authentication / Authorization (no-op) ──────────────────────────────
        // This app has no real auth. DisableAuthorization=true suppresses Elsa
        // Studio's own checks, but FastEndpoints / Elsa REST API registers ASP.NET
        // Core's AuthorizationMiddleware internally and its endpoints carry [Authorize]
        // policies. We satisfy both requirements with:
        //   • A passthrough auth scheme that always returns an authenticated identity
        //     (NoResult → 401, anonymous Success → 403, named Success → 200).
        //   • A default + fallback authorization policy that always asserts true,
        //     so every [Authorize] attribute on Elsa REST endpoints is satisfied.
        services.AddAuthentication(PassThroughAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, PassThroughAuthHandler>(
                    PassThroughAuthHandler.SchemeName, _ => { });
        // AllowAllAuthorizationHandler succeeds every IAuthorizationRequirement before
        // any policy or permission-claim check runs. This is needed because Elsa REST
        // API endpoints (FastEndpoints) use FastEndpoints' own permission-claim checks
        // which bypass a simple AddAuthorization fallback policy.
        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler,
                               AllowAllAuthorizationHandler>();

        // ─── API / ProblemDetails ─────────────────────────────────────────────────
        services.AddControllers();
        services.AddProblemDetails();
        services.AddExceptionHandler<DomainExceptionHandler>();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
    }

    /// <summary>
    /// Builds the HTTP pipeline. Call after <see cref="WebApplication.Build"/>.
    /// </summary>
    public static void ConfigurePipeline(WebApplication app)
    {
        // ProblemDetails middleware — RFC 9457 (ADR-011).
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // Elsa REST API (FastEndpoints).
        app.UseWorkflowsApi("elsa/api");

        // Elsa Studio (Blazor Server).
        app.UseStaticFiles();
        app.MapControllers();
        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");
    }
}

/// <summary>
/// No-op implementation of <see cref="IAuthenticationProviderManager"/> for use when
/// authorization is disabled. Elsa Studio workflow services require this interface to
/// be registered; the auth modules (ElsaIdentity, OIDC) normally provide it, but this
/// project opts out of authentication via <c>DisableAuthorization = true</c>.
/// </summary>
file sealed class NoOpAuthenticationProviderManager : IAuthenticationProviderManager
{
    public Task<string?> GetAuthenticationTokenAsync(
        string? tokenName,
        CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);
}

/// <summary>
/// Unconditionally succeeds every <see cref="IAuthorizationRequirement"/> before any
/// policy or claim-permission check runs. FastEndpoints enforces its own permission-claim
/// checks inside <c>IAuthorizationRequirement</c> handlers, so a simple policy-level
/// fallback is insufficient — we must short-circuit at the handler level.
/// </summary>
file sealed class AllowAllAuthorizationHandler
    : Microsoft.AspNetCore.Authorization.AuthorizationHandler<Microsoft.AspNetCore.Authorization.IAuthorizationRequirement>
{
    protected override Task HandleRequirementAsync(
        Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext context,
        Microsoft.AspNetCore.Authorization.IAuthorizationRequirement requirement)
    {
        context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Passthrough authentication handler — always returns an authenticated anonymous identity.
/// Required because FastEndpoints / Elsa REST API registers ASP.NET Core's
/// <c>AuthorizationMiddleware</c> internally, which needs a resolvable default challenge
/// scheme even when authorization is globally disabled via Elsa Studio's
/// <c>DisableAuthorization = true</c>.
/// </summary>
file sealed class PassThroughAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "PassThrough";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity  = new System.Security.Claims.ClaimsIdentity(SchemeName);
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);
        var ticket    = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
