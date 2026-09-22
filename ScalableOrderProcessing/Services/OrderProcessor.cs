using Microsoft.Extensions.Options;
using ScalableOrderProcessing.Models;
using ScalableOrderProcessing.Repositories;

namespace ScalableOrderProcessing.Services;

public class OrderProcessor : IOrderProcessor
{
    private readonly IOrderRepository _repo;
    private readonly IMockApiClient _apiClient;
    private readonly ILogger<OrderProcessor> _logger;
    private readonly WorkerOptions _options;
    private readonly TimeProvider _timeProvider;

    public OrderProcessor(
        IOrderRepository repo,
        IMockApiClient apiClient,
        ILogger<OrderProcessor> logger,
        IOptions<WorkerOptions> options,
        TimeProvider timeProvider)
    {
        _repo = repo;
        _apiClient = apiClient;
        _logger = logger;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task ProcessAsync(Order order, CancellationToken serviceStoppingToken)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(_options.JobTimeoutMinutes), _timeProvider);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(serviceStoppingToken, timeoutCts.Token);

        _logger.LogInformation("Processing order {OrderId}", order.Id);

        try
        {
            await _repo.MarkStartedAsync(order.Id, linkedCts.Token);
            await _apiClient.ProcessOrderAsync(order, linkedCts.Token);
            await _repo.MarkCompletedAsync(order.Id, CancellationToken.None);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            await _repo.MarkTimeoutAsync(order.Id, CancellationToken.None);
        }
        catch (OperationCanceledException) when (serviceStoppingToken.IsCancellationRequested)
        {
            await _repo.ResetToOpenAsync(order.Id, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Also catches cancellations that are neither our timeout nor the shutdown, e.g. HttpClient.Timeout
            _logger.LogError(ex, "Order {OrderId} failed", order.Id);
            await _repo.MarkFailedAsync(order.Id, CancellationToken.None);
        }
    }
}
