using ScalableOrderProcessing.Models;
using ScalableOrderProcessing.Services;

namespace ScalableOrderProcessing.Tests.Fakes;

public class FakeApiClient : IMockApiClient
{
    private readonly Func<Order, CancellationToken, Task> handler;

    private int _running;
    private int _started;
    private int _maxRunning;

    public int Started => _started;
    public int MaxRunning => _maxRunning;

    public FakeApiClient(Func<Order, CancellationToken, Task> handler)
    {
        this.handler = handler;
    }

    public async Task ProcessOrderAsync(Order order, CancellationToken ct)
    {
        Interlocked.Increment(ref _started);
        var running = Interlocked.Increment(ref _running);
        InterlockedMax(ref _maxRunning, running);
        try
        {
            await handler(order, ct);
        }
        finally
        {
            Interlocked.Decrement(ref _running);
        }
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while (value > (current = target) && Interlocked.CompareExchange(ref target, value, current) != current) { }
    }
}
