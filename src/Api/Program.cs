using Elsa.Extensions;
using Elsa.Workflow.Api.Middleware;
using Elsa.Workflow.Infrastructure.Extensions;
using Elsa.Workflow.Application.Commands;
using Elsa.Workflow.Application.Queries;
using Elsa.Workflow.Application.Workflows;
using Elsa.Workflow.Domain.Repositories;
using Elsa.Workflow.Infrastructure.Repositories;

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

    // Enable REST API endpoints for workflow operations
    elsa.UseWorkflowsApi();

    // Enable Identity for Studio authentication
    elsa.UseIdentity(identity => {
        identity.TokenOptions = options => options.SigningKey = "A-Super-Secret-Key-With-32-Chars!";
        identity.UseAdminUserProvider();
    });
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
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Elsa.Workflow.Api", Version = "v1" });
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Elsa.Workflow.Api v1");
    });
}

app.UseAuthentication();
app.UseAuthorization();

// Map customer API endpoints
app.MapControllers();

// Map Elsa workflow management and identity endpoints
app.UseWorkflowsApi();

app.Run();

// Required for WebApplicationFactory<Program> in functional tests.
public partial class Program { }
