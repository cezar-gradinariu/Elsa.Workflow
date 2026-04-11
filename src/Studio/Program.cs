using System.Text.Json;
using Elsa.Studio.Contracts;
using Elsa.Studio.Core.BlazorWasm.Extensions;
using Elsa.Studio.Dashboard.Extensions;
using Elsa.Studio.Extensions;
using Elsa.Studio.Login.BlazorWasm.Extensions;
using Elsa.Studio.Login.Extensions;
using Elsa.Studio.Login.HttpMessageHandlers;
using Elsa.Studio.Options;
using Elsa.Studio.Shell;
using Elsa.Studio.Shell.Extensions;
using Elsa.Studio.Workflows.Designer.Extensions;
using Elsa.Studio.Workflows.Extensions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Register root components.
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.RootComponents.RegisterCustomElsaStudioElements();

// Register shell, backend, auth, and feature modules.
builder.Services.AddCore();
builder.Services.AddShell();
builder.Services.AddRemoteBackend(new()
{
    ConfigureHttpClientBuilder = options =>
        options.AuthenticationHandler = typeof(AuthenticatingApiHttpMessageHandler)
});
builder.Services
    .AddLoginModule()
    .UseElsaIdentity();

builder.Services.AddDashboardModule();
builder.Services.AddWorkflowsModule();

// Build the application.
var app = builder.Build();

// Read the backend API URL injected by the host via getClientConfig().
var js = app.Services.GetRequiredService<IJSRuntime>();
var clientConfig = await js.InvokeAsync<JsonElement>("getClientConfig");
var apiUrl = clientConfig.GetProperty("apiUrl").GetString()
    ?? throw new InvalidOperationException("getClientConfig() did not return an apiUrl.");
app.Services.GetRequiredService<IOptions<BackendOptions>>().Value.Url = new Uri(apiUrl);

// Run registered startup tasks (module initialisation).
var startupTaskRunner = app.Services.GetRequiredService<IStartupTaskRunner>();
await startupTaskRunner.RunStartupTasksAsync();

await app.RunAsync();
