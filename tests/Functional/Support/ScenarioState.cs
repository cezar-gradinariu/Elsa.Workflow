using System.Net.Http.Json;
using System.Text.Json;
using Elsa.Workflow.Api.Models;

namespace Elsa.Workflow.Functional.Tests.Support;

/// <summary>
/// Per-scenario state bag. Injected into step definition classes via Reqnroll's DI.
/// Created fresh for each scenario — no shared mutable state between scenarios.
/// </summary>
public sealed class ScenarioState
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The fulfilment order ID created (or to be created) in this scenario.</summary>
    public Guid OrderId { get; set; }

    /// <summary>The raw HTTP response from the most recent API call.</summary>
    public HttpResponseMessage LastResponse
    {
        get => _lastResponse;
        set { _lastResponse = value; _cachedOrderResponse = null; }
    }
    private HttpResponseMessage _lastResponse = null!;
    private FulfilmentOrderResponse? _cachedOrderResponse;

    /// <summary>
    /// Deserialises the last response body as <see cref="FulfilmentOrderResponse"/>.
    /// Result is cached so the stream can be read multiple times within one scenario step sequence.
    /// </summary>
    public async Task<FulfilmentOrderResponse> ReadOrderResponseAsync() =>
        _cachedOrderResponse ??=
            (await LastResponse.Content.ReadFromJsonAsync<FulfilmentOrderResponse>(JsonOptions))!;
}
