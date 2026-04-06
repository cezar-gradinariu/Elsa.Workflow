using Elsa.Workflow.Api;

var builder = WebApplication.CreateBuilder(args);

// Required for _framework/blazor.server.js and _content/* to be served from
// NuGet/project static web assets in all environments (not just Development).
builder.WebHost.UseStaticWebAssets();

ProgramStartup.ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();
ProgramStartup.ConfigurePipeline(app);

app.Run();

// Required for WebApplicationFactory<Program> in functional tests.
public partial class Program { }
