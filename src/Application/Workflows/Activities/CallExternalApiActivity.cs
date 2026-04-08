using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Microsoft.Extensions.Logging;

namespace Elsa.Workflow.Application.Workflows.Activities;

/// <summary>
/// A custom activity that makes an HTTP GET call to an external API.
/// </summary>
public sealed class CallExternalApiActivity : CodeActivity
{
    public string Url { get; init; } = default!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        using var httpClient = new HttpClient();
        
        try
        {
            var logger = context.GetRequiredService<ILogger<CallExternalApiActivity>>();
            logger.LogInformation("Making API call to: {Url}", Url);

            var response = await httpClient.GetAsync(Url, context.CancellationToken);
            var content = await response.Content.ReadAsStringAsync(context.CancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("API call successful. Status: {StatusCode}, Response length: {Length} chars", 
                    response.StatusCode, content.Length);
            }
            else
            {
                logger.LogWarning("API call failed with status: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            var logger = context.GetRequiredService<ILogger<CallExternalApiActivity>>();
            logger.LogError(ex, "API call to {Url} failed with exception", Url);
        }
    }
}