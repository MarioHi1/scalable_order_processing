using System.Collections.Concurrent;
using ScalableOrderProcessing.Models;
using ScalableOrderProcessing.Repositories;

namespace ScalableOrderProcessing.Tests.Fakes;

public class FakeOrderRepository : IOrderRepository
{
    private readonly Queue<Order> _open;

    public ConcurrentDictionary<long, string> Status { get; } = new();
    public ConcurrentDictionary<long, bool> Started { get; } = new();

    public FakeOrderRepository(params long[] ids)
    {
        _open = new Queue<Order>(ids.Select(id => new Order { Id = id, Status = "Open" }));
    }

    public Task<List<Order>> FetchAndLockOpenOrdersAsync(int limit, CancellationToken ct)
    {
        lock (_open)
        {
            var orders = new List<Order>();
            while (orders.Count < limit && _open.TryDequeue(out var order))
            {
                order.Status = "InProgress";
                Status[order.Id] = "InProgress";
                orders.Add(order);
            }
            return Task.FromResult(orders);
        }
    }

    public Task MarkStartedAsync(long id, CancellationToken ct)
    {
        Started[id] = true;
        return Task.CompletedTask;
    }

    public Task MarkCompletedAsync(long id, CancellationToken ct) => Set(id, "Completed");
    public Task MarkTimeoutAsync(long id, CancellationToken ct) => Set(id, "Timeout");
    public Task MarkFailedAsync(long id, CancellationToken ct) => Set(id, "Failed");
    public Task ResetToOpenAsync(long id, CancellationToken ct)
    {
        Started.TryRemove(id, out _);
        return Set(id, "Open");
    }

    private Task Set(long id, string status)
    {
        Status[id] = status;
        return Task.CompletedTask;
    }
}
