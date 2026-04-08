using Elsa.Extensions;
using Elsa.Workflow.Api.Middleware;
using Elsa.Workflow.Infrastructure.Extensions;
using Elsa.Workflow.Application.Commands;
using Elsa.Workflow.Application.Queries;
using Elsa.Workflow.Application.Workflows;
using Elsa.Workflow.Domain.Repositories;
using Elsa.Workflow.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc.Controllers;

var builder = WebApplication.CreateBuilder(args);

// ─── MongoDB ─────────────────────────────────────────────────────────────────
// IMongoClient and IMongoDatabase are registered as singletons by Elsa's UseMongoDb.
// We do NOT register them here — doing so as Scoped would conflict with Elsa's
// singleton workflow stores (scope validation error in Development mode).
var mongoConnectionString = builder.Configuration["MongoDB:ConnectionString"]!;

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
builder.Services.AddElsa(elsa =>
{
    // MongoDB persistence for Elsa — wired via Infrastructure to keep
    // Elsa.Persistence.MongoDb references out of the Api layer.
    // UseMongoDb registers IMongoClient and IMongoDatabase as singletons.
    elsa.UseElsaMongoDb(mongoConnectionString);

    // Register code-first workflow definitions from the Application assembly.
    elsa.AddWorkflowsFrom<FulfilmentOrderWorkflow>();

    // Enable scheduling features for Delay and Timer activities.
    elsa.UseScheduling();

    // Enable HTTP features for HTTP request activities.
    elsa.UseHttp();

    // Enable REST API endpoints for workflow operations.
    // UseWorkflowsApi internally calls UseFastEndpoints and configures Elsa's
    // own authentication/authorization pipeline — do NOT add UseAuthentication /
    // UseAuthorization separately, as that fights Elsa's identity scheme setup.
    elsa.UseWorkflowsApi();

    // Enable Identity for Studio authentication (JWT via admin user provider).
    elsa.UseIdentity(identity => {
        identity.TokenOptions = options => options.SigningKey = "A-Super-Secret-Key-With-32-Chars!";
        identity.UseAdminUserProvider();
    });

    // Activates DefaultAuthenticationFeature which registers the "Jwt-or-ApiKey" policy
    // scheme as the default authentication/challenge scheme. Without this call the scheme
    // is never registered and UseAuthentication/UseAuthorization throw on every request.
    elsa.UseDefaultAuthentication();
});

// Add CORS — must allow the Elsa Studio Docker container (port 6002) and any
// local dev tooling. AllowAnyOrigin covers all cases in development.
builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy => policy
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowAnyOrigin()));

// ─── API / ProblemDetails ────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();

// Two Swagger docs: one for domain controllers, one for Elsa's FastEndpoints.
// We discriminate using ControllerActionDescriptor — our MVC controllers use it,
// Elsa's FastEndpoints do not. The old approach (Contains("Elsa")) broke because
// every action in this solution has "Elsa" in its display name due to the namespace.
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Domain API", Version = "v1" });
    c.SwaggerDoc("elsa", new() { Title = "Elsa Workflow API", Version = "v1" });
    c.DocInclusionPredicate((docName, api) =>
    {
        var isController = api.ActionDescriptor is ControllerActionDescriptor;
        return docName == "v1" ? isController : !isController;
    });
    c.CustomSchemaIds(type => type.FullName);
});

var app = builder.Build();

// CORS must be first so headers are present on every response, including
// error responses that the Studio sees during its initial handshake.
app.UseCors();

// ProblemDetails middleware — maps all unhandled exceptions and status codes
// to RFC 9457 problem+json.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        // Domain API listed first so it is the default selection in the dropdown.
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Domain API");
        c.SwaggerEndpoint("/swagger/elsa/swagger.json", "Elsa Workflow API");
    });
}

// Domain API endpoints (no auth required on our controllers).
app.MapControllers();

// Elsa workflow management and identity endpoints.
// UseWorkflowsApi wires up FastEndpoints with Elsa's own auth pipeline.
app.UseWorkflowsApi();

app.Run();

// Required for WebApplicationFactory<Program> in functional tests.
public partial class Program { }
