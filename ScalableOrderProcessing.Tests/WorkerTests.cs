using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using ScalableOrderProcessing.Repositories;
using ScalableOrderProcessing.Services;
using ScalableOrderProcessing.Tests.Fakes;

namespace ScalableOrderProcessing.Tests;

public class WorkerTests
{
    [Fact]
    public async Task Runs_at_most_MaxParallelJobs_orders_at_the_same_time()
    {
        var release = new TaskCompletionSource();
        var repo = new FakeOrderRepository(1, 2, 3, 4, 5);
        var api = new FakeApiClient((_, _) => release.Task);
        var worker = CreateWorker(repo, api, new FakeTimeProvider(), maxParallelJobs: 2);

        await worker.StartAsync(CancellationToken.None);

        await WaitUntil(() => api.Started == 2);
        await Task.Delay(200);
        Assert.Equal(2, api.Started);

        release.SetResult();
        await WaitUntil(() => repo.Status.Values.Count(s => s == "Completed") == 5);
        Assert.Equal(2, api.MaxRunning);

        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Marks_order_as_Timeout_when_the_api_takes_longer_than_JobTimeoutMinutes()
    {
        var time = new FakeTimeProvider();
        var repo = new FakeOrderRepository(1, 2);
        var api = new FakeApiClient((order, ct) =>
            order.Id == 1 ? Task.Delay(Timeout.Infinite, ct) : Task.CompletedTask);
        var worker = CreateWorker(repo, api, time, maxParallelJobs: 2, jobTimeoutMinutes: 5);

        await worker.StartAsync(CancellationToken.None);
        await WaitUntil(() => api.Started == 2);

        time.Advance(TimeSpan.FromMinutes(4));
        await Task.Delay(100);
        Assert.Equal("InProgress", repo.Status[1]);

        time.Advance(TimeSpan.FromMinutes(1));
        await WaitUntil(() => repo.Status[1] == "Timeout");
        Assert.Equal("Completed", repo.Status[2]);

        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Sets_started_at_only_when_a_worker_picks_the_order_from_the_queue()
    {
        var release = new TaskCompletionSource();
        var repo = new FakeOrderRepository(1, 2);
        var api = new FakeApiClient((_, _) => release.Task);
        var worker = CreateWorker(repo, api, new FakeTimeProvider(), maxParallelJobs: 1);

        await worker.StartAsync(CancellationToken.None);
        await WaitUntil(() => api.Started == 1);

        Assert.Equal("InProgress", repo.Status[2]);
        Assert.True(repo.Started.ContainsKey(1));
        Assert.False(repo.Started.ContainsKey(2));

        release.SetResult();
        await WaitUntil(() => repo.Started.ContainsKey(2));

        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Stopping_the_worker_resets_running_and_queued_orders_to_Open()
    {
        var repo = new FakeOrderRepository(1, 2, 3);
        var api = new FakeApiClient((_, ct) => Task.Delay(Timeout.Infinite, ct));
        var worker = CreateWorker(repo, api, new FakeTimeProvider(), maxParallelJobs: 1);

        await worker.StartAsync(CancellationToken.None);
        await WaitUntil(() => api.Started == 1);

        await worker.StopAsync(CancellationToken.None);

        Assert.Equal("Open", repo.Status[1]);
        Assert.Equal("Open", repo.Status[2]);
        Assert.Equal("Open", repo.Status[3]);
        Assert.Equal(1, api.Started);
    }

    [Fact]
    public async Task Marks_order_as_Failed_when_the_api_throws()
    {
        var repo = new FakeOrderRepository(1, 2);
        var api = new FakeApiClient((order, _) =>
            order.Id == 1 ? throw new InvalidOperationException("API call failed") : Task.CompletedTask);
        var worker = CreateWorker(repo, api, new FakeTimeProvider(), maxParallelJobs: 1);

        await worker.StartAsync(CancellationToken.None);

        await WaitUntil(() => repo.Status.GetValueOrDefault(2) == "Completed");
        Assert.Equal("Failed", repo.Status[1]);

        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Marks_order_as_Failed_when_the_api_is_cancelled_by_something_else()
    {
        // HttpClient.Timeout surfaces as a TaskCanceledException that is not tied to our tokens
        var repo = new FakeOrderRepository(1);
        var api = new FakeApiClient((_, _) => throw new TaskCanceledException("HttpClient.Timeout"));
        var worker = CreateWorker(repo, api, new FakeTimeProvider(), maxParallelJobs: 1);

        await worker.StartAsync(CancellationToken.None);

        await WaitUntil(() => repo.Status.GetValueOrDefault(1) == "Failed");

        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Releases_the_worker_slot_when_the_job_setup_throws()
    {
        var resolutions = 0;
        var repo = new FakeOrderRepository(1, 2);
        var api = new FakeApiClient((_, _) => Task.CompletedTask);
        var services = new ServiceCollection()
            .AddSingleton<IOrderRepository>(repo)
            .AddScoped<IMockApiClient>(_ => Interlocked.Increment(ref resolutions) == 1
                ? throw new InvalidOperationException("setup failed")
                : api);
        var worker = CreateWorker(services, new FakeTimeProvider(), maxParallelJobs: 1);

        await worker.StartAsync(CancellationToken.None);

        await WaitUntil(() => repo.Status.GetValueOrDefault(2) == "Completed");
        Assert.Equal("InProgress", repo.Status[1]);

        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Locks_at_most_MaxParallelJobs_plus_MaxQueueSize_orders()
    {
        var time = new FakeTimeProvider();
        var repo = new FakeOrderRepository(1, 2, 3, 4, 5, 6, 7, 8);
        var api = new FakeApiClient((_, ct) => Task.Delay(Timeout.Infinite, ct));
        var worker = CreateWorker(repo, api, time, maxParallelJobs: 1, maxQueueSize: 2);

        await worker.StartAsync(CancellationToken.None);
        await WaitUntil(() => api.Started == 1);

        for (var i = 0; i < 3; i++)
        {
            await Task.Delay(100);
            time.Advance(TimeSpan.FromSeconds(10));
        }
        await Task.Delay(100);

        Assert.Equal(3, repo.Status.Values.Count(s => s == "InProgress"));

        await worker.StopAsync(CancellationToken.None);
    }

    private static Worker CreateWorker(
        IOrderRepository repo,
        IMockApiClient api,
        TimeProvider time,
        int maxParallelJobs,
        int jobTimeoutMinutes = 5,
        int maxQueueSize = 20)
    {
        var services = new ServiceCollection()
            .AddSingleton(repo)
            .AddSingleton(api);

        return CreateWorker(services, time, maxParallelJobs, jobTimeoutMinutes, maxQueueSize);
    }

    private static Worker CreateWorker(
        IServiceCollection services,
        TimeProvider time,
        int maxParallelJobs,
        int jobTimeoutMinutes = 5,
        int maxQueueSize = 20)
    {
        var options = Options.Create(new WorkerOptions
        {
            PollIntervalSeconds = 10,
            MaxQueueSize = maxQueueSize,
            MaxParallelJobs = maxParallelJobs,
            JobTimeoutMinutes = jobTimeoutMinutes,
        });

        var provider = services
            .AddLogging()
            .AddSingleton(options)
            .AddSingleton(time)
            .AddScoped<IOrderProcessor, OrderProcessor>()
            .BuildServiceProvider();

        return new Worker(
            NullLogger<Worker>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>(),
            options,
            time);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Condition not met within 5 seconds");
            await Task.Delay(10);
        }
    }
}
