using Elsa.Resilience;
using Elsa.Resilience.Extensions;
using Elsa.Workflow.Application.Resilience;
using FastEndpoints.Swagger;
using Microsoft.OpenApi;
using Microsoft.Extensions.Hosting;
using Elsa.Extensions;
using Elsa.Workflow.Api.Middleware;
using Elsa.Workflow.Infrastructure.Extensions;
using Elsa.Workflow.Application.Commands;
using Elsa.Workflow.Application.Queries;
using Elsa.Workflow.Application.Workflows;
using Elsa.Workflow.Domain.Repositories;
using Elsa.Workflow.Infrastructure.Repositories;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// Elsa background services (JobRunnerHostedService, BackgroundEventPublisherHostedService)
// throw OperationCanceledException / ObjectDisposedException during graceful shutdown —
// these are expected teardown artifacts, not real failures.
builder.Services.Configure<HostOptions>(o =>
    o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

// ─── MongoDB ─────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(builder.Configuration["MongoDB:ConnectionString"]));

builder.Services.AddScoped<IMongoDatabase>(sp =>
    sp.GetRequiredService<IMongoClient>()
      .GetDatabase(builder.Configuration["MongoDB:DatabaseName"]));

// ─── Domain / Application ────────────────────────────────────────────────────
builder.Services.AddScoped<IFulfilmentOrderRepository, FulfilmentOrderRepository>();

builder.Services.AddScoped<
    ICommandHandler<CreateFulfilmentOrderCommand>,
    CreateFulfilmentOrderCommandHandler>();

builder.Services.AddScoped<
    ICommandHandler<DeleteFulfilmentOrderCommand>,
    DeleteFulfilmentOrderCommandHandler>();

builder.Services.AddScoped<
    IQueryHandler<GetFulfilmentOrderQuery, GetFulfilmentOrderResult>,
    GetFulfilmentOrderQueryHandler>();

// ─── Elsa Workflows ──────────────────────────────────────────────────────────
var connectionString = builder.Configuration["MongoDB:ConnectionString"]!;
var databaseName     = builder.Configuration["MongoDB:DatabaseName"]!;

builder.Services.AddElsa(elsa =>
{
    // MongoDB persistence for Elsa — wired via Infrastructure to keep
    // Elsa.Persistence.MongoDb references out of the Api layer (ADR-001/003).
    elsa.UseElsaMongoDb(connectionString, databaseName);

    // JWT-based identity: provides /elsa/api/identity/login and secures all
    // Elsa REST endpoints with Bearer tokens. Default credentials: admin / password.
    elsa.UseIdentity(identity =>
    {
        identity.TokenOptions = opts =>
        {
            opts.SigningKey = builder.Configuration["ElsaIdentity:SigningKey"]!;
            opts.AccessTokenLifetime = TimeSpan.FromDays(1);
        };
        identity.UseAdminUserProvider();
    });

    // Registers the default JWT+ApiKey policy scheme as the ASP.NET Core default
    // authentication scheme. Without this UseAuthorization() has no default
    // challenge scheme and throws InvalidOperationException on every protected request.
    elsa.UseDefaultAuthentication();

    // Elsa.Resilience — Level-1 fast retry + circuit breaker for workflow activities.
    // AddResilienceStrategyType registers OfaResilienceStrategy for JSON serialization
    // so the Studio can discover and display it in the strategy picker (ADR-006).
    elsa.UseResilience(r => r.AddResilienceStrategyType(typeof(OfaResilienceStrategy)));

    // Expose the Elsa REST API (FastEndpoints) consumed by the embedded Studio (ADR-007).
    elsa.UseWorkflowsApi(_ => { });

    // Register code-first workflow definitions from the Application assembly.
    elsa.AddWorkflowsFrom<FulfilmentOrderWorkflow>();
});

// ─── Resilience strategy source ──────────────────────────────────────────────
// Makes OfaResilienceStrategy discoverable by IResilienceStrategyCatalog so that
// CallOfaActivity can reference it by ID and the Studio can list it (ADR-006).
builder.Services.AddScoped<IResilienceStrategySource, ApplicationResilienceStrategySource>();

// ─── External HTTP clients ────────────────────────────────────────────────────
// Named HttpClient("OFA"): base address only — no resilience handler here.
// Retry and circuit breaker are owned by OfaResilienceStrategy and applied
// transparently by IResilientActivityInvoker inside CallOfaActivity (ADR-006).
var ofaBaseUrl = builder.Configuration["ExternalApis:Ofa:BaseUrl"]!;
builder.Services.AddOfaHttpClient(ofaBaseUrl);

// ─── API / ProblemDetails ────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Fulfilment API", Version = "v1" });
    // FastEndpoints 7.x registers through endpoint routing, so IApiDescriptionProvider
    // surfaces Elsa endpoints to Swashbuckle. Restrict this document to our own
    // MVC controllers only; Elsa endpoints are covered by the NSwag doc below.
    c.DocInclusionPredicate((_, api) =>
        api.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor);
    // Bearer auth — OpenApi 2.x types live in Microsoft.OpenApi (no Models sub-namespace).
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter JWT token (obtain from POST /elsa/api/identity/login with admin / password)"
    });
    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer", doc, null), [] }
    });
});

// FastEndpoints.Swagger (NSwag) document for Elsa's workflow management endpoints.
// 7.1.1 is already a transitive dep of Elsa.Workflows.Api — no extra package needed.
builder.Services.SwaggerDocument(o =>
{
    o.DocumentSettings = s =>
    {
        s.DocumentName = "elsa";
        s.Title = "Elsa Workflows API";
        s.Version = "v1";
    };
    o.ExcludeNonFastEndpoints = true; // only Elsa FastEndpoints, not our controllers
    o.EnableJWTBearerAuth = true;
    o.ShortSchemaNames = true;
});

var app = builder.Build();

// ProblemDetails middleware — maps all unhandled exceptions and status codes
// to RFC 9457 problem+json (ADR-011).
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    // Swashbuckle: serves our controller endpoints at /swagger/v1/swagger.json.
    app.UseSwagger();

    // NSwag/FastEndpoints.Swagger: serves Elsa endpoints at /openapi/elsa/swagger.json.
    // The /openapi prefix avoids Swashbuckle intercepting /swagger/{name} requests
    // and returning "Unknown Swagger document" before NSwag can handle them.
    app.UseSwaggerGen(
        cfg => cfg.Path = "/openapi/{documentName}/swagger.json",
        ui  => ui.Path  = "/openapi/ui"); // FastEndpoints' own UI (not advertised)

    // Unified Swagger UI with both documents in the dropdown.
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Fulfilment API");
        c.SwaggerEndpoint("/openapi/elsa/swagger.json", "Elsa Workflows API");
    });
}

// Redirect root to the Studio entry point (trailing slash required so the
// browser sets the correct base for relative asset resolution with
// <base href="/studio/"> in index.html).
app.MapGet("/", () => Results.Redirect("/studio/"));

// Elsa Studio navigates to the absolute path /login when unauthenticated.
// With <base href="/studio/"> the Blazor router expects /studio/login, so
// redirect the bare /login hit to the correct Studio-relative path.
app.MapGet("/login", () => Results.Redirect("/studio/login"));

// Serve Elsa Studio WASM static assets.
// Two UseStaticFiles registrations are needed:
//   1. Root (/): serves /_framework/, /_content/ etc. for direct/cached requests.
//   2. /studio prefix: serves the same files under /studio/_framework/ etc. so
//      that relative asset paths in index.html resolve correctly when
//      <base href="/studio/"> is in effect.
// ServeUnknownFileTypes covers Blazor WASM ICU .dat files which have no
// registered MIME type in the default FileExtensionContentTypeProvider.
var staticFileOptions = new StaticFileOptions
{
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream"
};
app.UseStaticFiles(staticFileOptions);
app.UseStaticFiles(new StaticFileOptions
{
    RequestPath = "/studio",
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream"
});

// UseRouting must precede UseAuthentication/UseAuthorization so the selected
// endpoint is known when auth middleware runs (matches Elsa reference sample).
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ─── Elsa REST API ────────────────────────────────────────────────────────────
// Exposes workflow management endpoints at /elsa/api consumed by the Studio.
// UseWorkflowsApi (UseFastEndpoints) is used — not MapWorkflowsApi — because
// FastEndpoints' permission-based authorization is handled internally by the
// UseFastEndpoints middleware. MapFastEndpoints hands permission checks to
// UseAuthorization() which lacks FastEndpoints' permission handlers → 401.
// Both /elsa/api and /studio should be blocked at the ingress on the
// customer-facing listener — do not rely solely on application-level auth.
app.UseWorkflowsApi("elsa/api");

// SPA fallback: any /studio/* request that does not match a file is served
// index.html so Blazor client-side routing works for all Shell routes.
// With <base href="/studio/"> the Shell routes to /studio/login,
// /studio/workflows etc. which all match this pattern.
app.MapFallbackToFile("studio/{**path:nonfile}", "index.html");

app.Run();

// Required for WebApplicationFactory<Program> in functional tests.
public partial class Program { }
