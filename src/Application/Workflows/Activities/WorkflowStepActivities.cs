using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
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
    // Input<T>(Variable) uses the public non-generic Variable base class constructor.
    // Do NOT use Input<T>(Variable<T>) — that resolves to the protected
    // Input(MemoryBlockReference, Type) overload and causes a compile error.
    [Input(Description = "The API response captured from CallExternalApiActivity")]
    public Input<string>? ApiResponse { get; set; }

    protected override ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var logger = context.GetRequiredService<ILogger<ApiCallCompletedActivity>>();
        var response = context.Get(ApiResponse);

        // JournalData is stored in the ActivityExecutionRecord — visible in Studio's
        // execution log under this activity's "Journal" tab.
        context.JournalData["ApiResponse"] = response ?? "(empty)";

        logger.LogInformation("API call completed. Response: {Response}", response);
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

public sealed class ApprovalGrantedActivity : CodeActivity
{
    protected override ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var logger = context.GetRequiredService<ILogger<ApprovalGrantedActivity>>();
        logger.LogInformation("✅ APPROVAL GRANTED: FulfilmentOrder has been approved. Proceeding.");
        Console.WriteLine("✅ APPROVAL GRANTED: FulfilmentOrder has been approved. Proceeding.");
        return ValueTask.CompletedTask;
    }
}

public sealed class ApprovalRejectedActivity : CodeActivity
{
    protected override ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var logger = context.GetRequiredService<ILogger<ApprovalRejectedActivity>>();
        logger.LogInformation("❌ APPROVAL REJECTED: FulfilmentOrder was rejected. Terminating workflow.");
        Console.WriteLine("❌ APPROVAL REJECTED: FulfilmentOrder was rejected. Terminating workflow.");
        return ValueTask.CompletedTask;
    }
}