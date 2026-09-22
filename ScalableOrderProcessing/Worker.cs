using System.Threading.Channels;
using Microsoft.Extensions.Options;
using ScalableOrderProcessing.Models;
using ScalableOrderProcessing.Repositories;
using ScalableOrderProcessing.Services;

namespace ScalableOrderProcessing;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WorkerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly Channel<Order> _queue;
    private readonly SemaphoreSlim _semaphore;

    public Worker(
        ILogger<Worker> logger,
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerOptions> options,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _timeProvider = timeProvider;

        _queue = Channel.CreateBounded<Order>(new BoundedChannelOptions(_options.MaxQueueSize)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        _semaphore = new SemaphoreSlim(_options.MaxParallelJobs, _options.MaxParallelJobs);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Worker started (PollIntervalSeconds={PollIntervalSeconds}, MaxQueueSize={MaxQueueSize}, MaxParallelJobs={MaxParallelJobs}, JobTimeoutMinutes={JobTimeoutMinutes})",
            _options.PollIntervalSeconds, _options.MaxQueueSize, _options.MaxParallelJobs, _options.JobTimeoutMinutes);

        var pollTask = PollLoopAsync(stoppingToken);
        var dispatchTask = DispatchLoopAsync(stoppingToken);

        await Task.WhenAll(pollTask, dispatchTask);

        // Drain only now: both loops have finished, so nothing else can enter the queue
        await ResetQueuedOrdersAsync();

        // Every job releases its slot in a finally, so holding all slots means every job has finished
        var running = _options.MaxParallelJobs - _semaphore.CurrentCount;
        if (running > 0)
        {
            _logger.LogInformation("Waiting for {RunningOrders} running orders...", running);
        }

        for (var i = 0; i < _options.MaxParallelJobs; i++)
        {
            await _semaphore.WaitAsync(CancellationToken.None);
        }

        _logger.LogInformation("Worker stopped");
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollIntervalSeconds), _timeProvider);

        try
        {
            do
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

                    var freeSlots = _options.MaxQueueSize - _queue.Reader.Count;

                    if (freeSlots > 0)
                    {
                        var orders = await repo.FetchAndLockOpenOrdersAsync(freeSlots, ct);
                        foreach (var order in orders)
                        {
                            // Only this loop writes to the queue, so the free slots are guaranteed.
                            // No WriteAsync(ct): cancelling in the middle of the loop would lose the
                            // remaining orders that are already locked.
                            if (!_queue.Writer.TryWrite(order))
                            {
                                _logger.LogWarning("Order {OrderId} does not fit into the queue, resetting", order.Id);
                                await repo.ResetToOpenAsync(order.Id, CancellationToken.None);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Queue full ({QueueSize}/{MaxQueueSize}), skipping poll", _queue.Reader.Count, _options.MaxQueueSize);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error while fetching open orders");
                }
            }
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested
        }
        finally
        {
            _queue.Writer.TryComplete();
        }
    }

    private async Task DispatchLoopAsync(CancellationToken ct)
    {
        try
        {
            while (await _queue.Reader.WaitToReadAsync(ct))
            {
                // Take the slot first and only then read: the order stays in the queue until it can
                // start, so the poll loop never fetches more than MaxQueueSize waiting orders
                await _semaphore.WaitAsync(ct);

                if (!_queue.Reader.TryRead(out var order))
                {
                    _semaphore.Release();
                    continue;
                }

                _logger.LogInformation("Active workers: {ActiveWorkers}/{MaxParallelJobs}", _options.MaxParallelJobs - _semaphore.CurrentCount, _options.MaxParallelJobs);

                _ = ProcessOrderAsync(order, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested
        }
    }

    private async Task ResetQueuedOrdersAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

        while (_queue.Reader.TryRead(out var leftover))
        {
            await repo.ResetToOpenAsync(leftover.Id, CancellationToken.None);
        }
    }

    private async Task ProcessOrderAsync(Order order, CancellationToken serviceStoppingToken)
    {
        // Every log of this job, including the ones in the repository and the API client, carries the OrderId
        using var logScope = _logger.BeginScope("Order {OrderId}", order.Id);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var apiClient = scope.ServiceProvider.GetRequiredService<IMockApiClient>();

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(_options.JobTimeoutMinutes), _timeProvider);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(serviceStoppingToken, timeoutCts.Token);

            _logger.LogInformation("Processing order {OrderId}", order.Id);

            try
            {
                await repo.MarkStartedAsync(order.Id, linkedCts.Token);
                await apiClient.ProcessOrderAsync(order, linkedCts.Token);
                await repo.MarkCompletedAsync(order.Id, CancellationToken.None);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                await repo.MarkTimeoutAsync(order.Id, CancellationToken.None);
            }
            catch (OperationCanceledException) when (serviceStoppingToken.IsCancellationRequested)
            {
                await repo.ResetToOpenAsync(order.Id, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while processing order {OrderId}", order.Id);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
