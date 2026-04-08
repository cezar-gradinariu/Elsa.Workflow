using Elsa.Workflows;
using Microsoft.Extensions.Logging;

namespace Elsa.Workflow.Application.Workflows.Activities;

public sealed class WorkflowStartedActivity : CodeActivity
{
    protected override ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var logger = context.GetRequiredService<ILogger<WorkflowStartedActivity>>();
        logger.LogInformation("🚀 STEP 1: FulfilmentOrder workflow started - processing order");
        return ValueTask.CompletedTask;
    }
}

public sealed class PrepareDelayActivity : CodeActivity
{
    protected override ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var logger = context.GetRequiredService<ILogger<PrepareDelayActivity>>();
        logger.LogInformation("⏱️ STEP 2: Starting 1-minute delay timer...");
        return ValueTask.CompletedTask;
    }
}

public sealed class PrepareApiCallActivity : CodeActivity
{
    protected override ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        context.ActivityDescriptor.DisplayName = "⏰ Timer completed - preparing to call external API";
        context.ActivityDescriptor.Description = "This activity is triggered after the 1-minute delay timer completes. It prepares for the next step, which is calling an external API.";
        var logger = context.GetRequiredService<ILogger<PrepareApiCallActivity>>();
        logger.LogInformation("⏰ STEP 3: Timer completed! Now calling external API...");
        return ValueTask.CompletedTask;
    }
}

public sealed class ApiCallCompletedActivity : CodeActivity
{
    protected override ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var logger = context.GetRequiredService<ILogger<ApiCallCompletedActivity>>();
        logger.LogInformation("📡 STEP 4: HTTP request to JSONPlaceholder API completed");
        return ValueTask.CompletedTask;
    }
}

public sealed class BeginFinalizationActivity : CodeActivity
{
    protected override ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var logger = context.GetRequiredService<ILogger<BeginFinalizationActivity>>();
        logger.LogInformation("🏁 STEP 5: Beginning workflow finalization process...");
        return ValueTask.CompletedTask;
    }
}

public sealed class WorkflowCompletedActivity : CodeActivity
{
    protected override ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var logger = context.GetRequiredService<ILogger<WorkflowCompletedActivity>>();
        logger.LogInformation("✅ STEP 6: SUCCESS! FulfilmentOrder workflow completed successfully");
        return ValueTask.CompletedTask;
    }
}