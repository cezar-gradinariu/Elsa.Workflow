using System.Net;
using System.Net.Http.Json;
using Elsa.Workflow.Api.Models;
using Elsa.Workflow.Functional.Tests.Support;
using Reqnroll;
using Xunit;

namespace Elsa.Workflow.Functional.Tests.StepDefinitions;

[Binding]
public sealed class FulfilmentOrderSteps(HttpClient client, ScenarioState state)
{
    // -------------------------------------------------------------------------
    // Background
    // -------------------------------------------------------------------------

    [Given("a new fulfilment order ID is generated")]
    public void GivenANewFulfilmentOrderIdIsGenerated()
    {
        state.OrderId = Guid.NewGuid();
    }

    // -------------------------------------------------------------------------
    // Given — preconditions that POST an order before the scenario's When step
    // -------------------------------------------------------------------------

    [Given(@"a fulfilment order has been created for store ""(.*)"" with order ID ""(.*)""")]
    public async Task GivenAFulfilmentOrderHasBeenCreated(string storeId, string orderId)
    {
        var request  = SingleLineRequest(storeId, orderId);
        var response = await client.PostAsJsonAsync("/api/fulfilments", request);
        response.EnsureSuccessStatusCode();
    }

    // -------------------------------------------------------------------------
    // When
    // -------------------------------------------------------------------------

    [When(@"I create a fulfilment order for store ""(.*)"" with the following lines:")]
    public async Task WhenICreateAFulfilmentOrderWithLines(string storeId, Table table)
    {
        var lines = table.Rows.Select(row => new TestOrderLineRequest(
            OrderLineNo:               int.Parse(row["OrderLineNo"]),
            ArticleId:                 row["ArticleId"],
            ExpectedQuantity:          decimal.Parse(row["ExpectedQuantity"]),
            UnitOfMeasure:             row["UnitOfMeasure"],
            CustomerSupplyInstructions: NullIfEmpty(row["CustomerSupplyInstructions"])
        )).ToArray();

        var request = new CreateOrderRequest(state.OrderId, storeId, "ORD-001", lines);
        state.LastResponse = await client.PostAsJsonAsync("/api/fulfilments", request);
    }

    [When("I retrieve the order by its ID")]
    public async Task WhenIRetrieveTheOrderByItsId()
    {
        state.LastResponse = await client.GetAsync($"/api/fulfilments/{state.OrderId}");
    }

    [When("I delete the order by its ID")]
    public async Task WhenIDeleteTheOrderByItsId()
    {
        state.LastResponse = await client.DeleteAsync($"/api/fulfilments/{state.OrderId}");
    }

    // -------------------------------------------------------------------------
    // Then — status code
    // -------------------------------------------------------------------------

    [Then(@"the response status is (\d+) (.+)")]
    public void ThenTheResponseStatusIs(int statusCode, string _)
    {
        Assert.Equal((HttpStatusCode)statusCode, state.LastResponse.StatusCode);
    }

    [Then(@"retrieving the order by its ID returns (\d+) (.+)")]
    public async Task ThenRetrievingTheOrderReturns(int statusCode, string _)
    {
        var response = await client.GetAsync($"/api/fulfilments/{state.OrderId}");
        Assert.Equal((HttpStatusCode)statusCode, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Then — body assertions
    // -------------------------------------------------------------------------

    [Then(@"the order can be retrieved with status ""(.*)""")]
    public async Task ThenTheOrderCanBeRetrievedWithStatus(string expectedStatus)
    {
        var body = await GetOrderAsync();
        Assert.Equal(state.OrderId, body.FulfilmentOrderId);
        Assert.Equal(expectedStatus, body.Status);
    }

    [Then(@"the order can be retrieved with (\d+) order lines")]
    public async Task ThenTheOrderCanBeRetrievedWithOrderLines(int expectedLineCount)
    {
        var body = await GetOrderAsync();
        Assert.Equal(expectedLineCount, body.OrderLines.Count);
    }

    [Then(@"all order lines have allocation status ""(.*)""")]
    public async Task ThenAllOrderLinesHaveAllocationStatus(string expectedStatus)
    {
        var body = await GetOrderAsync();
        Assert.All(body.OrderLines, line => Assert.Equal(expectedStatus, line.AllocationStatus));
    }

    [Then(@"the order line (\d+) has allocation status ""(.*)""")]
    public async Task ThenOrderLineHasAllocationStatus(int lineNo, string expectedStatus)
    {
        var body = await GetOrderAsync();
        var line  = body.OrderLines.Single(l => l.OrderLineNo == lineNo);
        Assert.Equal(expectedStatus, line.AllocationStatus);
    }

    [Then(@"the order line (\d+) has customer supply instructions ""(.*)""")]
    public async Task ThenOrderLineHasCustomerSupplyInstructions(int lineNo, string expected)
    {
        var body = await GetOrderAsync();
        var line  = body.OrderLines.Single(l => l.OrderLineNo == lineNo);
        Assert.Equal(expected, line.CustomerSupplyInstructions);
    }

    [Then(@"the response body contains store ""(.*)""")]
    public async Task ThenResponseBodyContainsStore(string expectedStoreId)
    {
        var body = await state.ReadOrderResponseAsync();
        Assert.Equal(expectedStoreId, body.StoreId);
    }

    [Then(@"the response body contains order ID ""(.*)""")]
    public async Task ThenResponseBodyContainsOrderId(string expectedOrderId)
    {
        var body = await state.ReadOrderResponseAsync();
        Assert.Equal(expectedOrderId, body.OrderId);
    }

    [Then("the order version is 0")]
    public async Task ThenTheOrderVersionIsZero()
    {
        var body = await state.ReadOrderResponseAsync();
        Assert.Equal(0, body.Version);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<FulfilmentOrderResponse> GetOrderAsync()
    {
        var response = await client.GetAsync($"/api/fulfilments/{state.OrderId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FulfilmentOrderResponse>())!;
    }

    private CreateOrderRequest SingleLineRequest(string storeId, string orderId) =>
        new(
            FulfilmentOrderId: state.OrderId,
            StoreId:           storeId,
            OrderId:           orderId,
            OrderLines:
            [
                new TestOrderLineRequest(1, "SKU-001", 3.0m, "Ea", null)
            ]);

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
