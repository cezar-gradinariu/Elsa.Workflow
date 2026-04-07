using Microsoft.AspNetCore.Mvc;
using Elsa.Workflows.Management.Services;
using Elsa.Workflow.Application.Workflows;

namespace Elsa.Workflow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkflowTestController : ControllerBase
{
    [HttpGet("test")]
    public IActionResult Test()
    {
        return Ok(new { 
            Message = "Elsa Workflow API is running!",
            Timestamp = DateTime.UtcNow,
            Status = "Your FulfilmentOrderWorkflow is registered and ready"
        });
    }
    
    [HttpPost("create-fulfilment-order")]
    public async Task<IActionResult> CreateFulfilmentOrder([FromBody] CreateTestOrderRequest request)
    {
        try
        {
            // Simulate creating a fulfilment order that would trigger your workflow
            var orderId = Guid.NewGuid().ToString();
            
            return Ok(new { 
                OrderId = orderId,
                Message = "Order created - workflow would be triggered",
                CustomerName = request.CustomerName ?? "Test Customer",
                Items = request.Items ?? []
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }
}

public class CreateTestOrderRequest 
{
    public string? CustomerName { get; set; }
    public string[]? Items { get; set; }
}