using Elsa.Workflows;
using Elsa.Workflows.Activities.Flowchart.Attributes;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Microsoft.Extensions.Logging;

namespace Elsa.Workflow.Application.Workflows.Activities;

// 1. DISPLAY & DESCRIPTION: Set them explicitly in the Attribute
[Activity(
    Namespace = "MyCustomActivities",
    DisplayName = "Call an External API", 
    Description = "Executes a GET request and returns the body content.",
    Category = "Networking"
)]
[FlowNode("Success", "Failed")] // OUTCOMES: The ports for the flowchart
public sealed class CallExternalApiActivity : CodeActivity
{
    [Input(Description = "The URL to call")]
    public Input<string> Url { get; set; } = default!;

    // 2. OUTPUT: This allows other activities to "see" and use the response data
    [Output(Description = "The JSON or text returned from the API")]
    public Output<string>? Result { get; set; }

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var url = context.Get(Url);
        var logger = context.GetRequiredService<ILogger<CallExternalApiActivity>>();
        
        using var httpClient = new HttpClient();
        
        try
        {
            var response = await httpClient.GetAsync(url, context.CancellationToken);
            var content = await response.Content.ReadAsStringAsync();

            // 3. SETTING THE OUTPUT: This maps the data to the 'Result' property
            context.Set(Result, content);
            
            // This also helps with debugging in the 'Execution Log'
            context.JournalData.Add("Response Body", content);

            if (response.IsSuccessStatusCode)
            {
                await context.CompleteActivityWithOutcomesAsync("Success");
            }
            else
            {
                await context.CompleteActivityWithOutcomesAsync("Failed");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "API Call failed");
            await context.CompleteActivityWithOutcomesAsync("Failed");
        }
    }
}