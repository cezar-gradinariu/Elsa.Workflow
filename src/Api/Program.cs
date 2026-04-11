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

    // Expose the Elsa REST API (FastEndpoints) consumed by the embedded Studio (ADR-007).
    elsa.UseWorkflowsApi(_ => { });

    // Register code-first workflow definitions from the Application assembly.
    elsa.AddWorkflowsFrom<FulfilmentOrderWorkflow>();
});

// ─── API / ProblemDetails ────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ProblemDetails middleware — maps all unhandled exceptions and status codes
// to RFC 9457 problem+json (ADR-011).
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Redirect root so http://localhost:5158 lands somewhere useful.
app.MapGet("/", () => Results.Redirect("/studio"));

// Serve Elsa Studio WASM static assets.
// Static web assets from the co-built Studio project are picked up automatically
// by UseStaticFiles() in .NET 8+ — UseBlazorFrameworkFiles() is no longer needed.
app.UseStaticFiles();

app.UseAuthorization();
app.MapControllers();

// ─── Elsa REST API ────────────────────────────────────────────────────────────
// Exposes workflow management endpoints at /elsa/api consumed by the Studio.
// Both /elsa/api and /studio should be blocked at the ingress on the
// customer-facing listener — do not rely solely on application-level auth.
app.UseWorkflowsApi("elsa/api");

// SPA fallback: any /studio/* request that does not match a file or API route
// is handed to the Studio index.html so client-side routing works.
app.MapFallbackToFile("studio/{**path:nonfile}", "index.html");

app.Run();

// Required for WebApplicationFactory<Program> in functional tests.
public partial class Program { }
