using System.Net.Http.Json;
using ScalableOrderProcessing.Models;

namespace ScalableOrderProcessing.Services;

public class MockApiClient : IMockApiClient
{
    private static readonly string[] Endpoints =
    [
        "service-a/1",
        "service-a/2",
        "service-a/3",
        "service-b/1",
        "service-b/2"
    ];

    private readonly HttpClient _httpClient;
    private readonly ILogger<MockApiClient> _logger;

    public MockApiClient(HttpClient httpClient, ILogger<MockApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task ProcessOrderAsync(Order order, CancellationToken ct)
    {
        var payload = new
        {
            id = Guid.NewGuid().ToString(),
            createdAt = order.CreatedAt,
            status = "InProgress"
        };

        var calls = Endpoints.Select(endpoint => CallEndpointAsync(endpoint, payload, ct));
        await Task.WhenAll(calls);
    }

    private async Task CallEndpointAsync(string endpoint, object payload, CancellationToken ct)
    {
        var response = await _httpClient.PostAsJsonAsync(endpoint, payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"API call {endpoint} failed: {(int)response.StatusCode} - {body}");
        }

        _logger.LogDebug("API call {Endpoint} succeeded with {StatusCode}", endpoint, (int)response.StatusCode);
    }
}