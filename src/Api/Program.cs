using Elsa.Workflow.Api;

var builder = WebApplication.CreateBuilder(args);
ProgramStartup.ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();
ProgramStartup.ConfigurePipeline(app);

app.Run();

// Required for WebApplicationFactory<Program> in functional tests.
public partial class Program { }
