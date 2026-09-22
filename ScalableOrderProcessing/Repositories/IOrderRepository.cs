using ScalableOrderProcessing.Models;

namespace ScalableOrderProcessing.Repositories;

public interface IOrderRepository
{
    Task<List<Order>> FetchAndLockOpenOrdersAsync(int limit, CancellationToken ct);
    Task MarkStartedAsync(long id, CancellationToken ct);
    Task MarkCompletedAsync(long id, CancellationToken ct);
    Task MarkTimeoutAsync(long id, CancellationToken ct);
    Task MarkFailedAsync(long id, CancellationToken ct);
    Task ResetToOpenAsync(long id, CancellationToken ct);
}