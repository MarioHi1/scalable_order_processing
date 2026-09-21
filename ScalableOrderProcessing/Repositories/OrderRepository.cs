using Npgsql;
using ScalableOrderProcessing.Models;

namespace ScalableOrderProcessing.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<OrderRepository> _logger;

    public OrderRepository(NpgsqlDataSource dataSource, ILogger<OrderRepository> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task<List<Order>> FetchAndLockOpenOrdersAsync(int limit, CancellationToken ct)
    {
        if (limit <= 0)
            return [];

        const string sql = """
            UPDATE orders
            SET status = 'InProgress'
            WHERE id IN (
                SELECT id FROM orders
                WHERE status = 'Open'
                ORDER BY id
                FOR UPDATE SKIP LOCKED
                LIMIT $1
            )
            RETURNING id, status, created_at, started_at, finished_at, timeout_at;
            """;

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(limit);

        var orders = new List<Order>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            orders.Add(new Order
            {
                Id = reader.GetInt64(0),
                Status = reader.GetString(1),
                CreatedAt = reader.GetFieldValue<DateTimeOffset>(2),
                StartedAt = reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3),
                FinishedAt = reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
                TimeoutAt = reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
            });
        }

        if (orders.Count > 0)
        {
            _logger.LogInformation(
                "{OrderCount} orders found and claimed: {OrderIds}",
                orders.Count, string.Join(", ", orders.Select(o => o.Id)));
        }

        return orders;
    }

    public async Task MarkStartedAsync(long id, CancellationToken ct)
    {
        const string sql = "UPDATE orders SET started_at = NOW() WHERE id = $1";
        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task MarkCompletedAsync(long id, CancellationToken ct)
    {
        const string sql = "UPDATE orders SET status = 'Completed', finished_at = NOW() WHERE id = $1";
        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);
        await cmd.ExecuteNonQueryAsync(ct);
        _logger.LogInformation("Order {OrderId} completed", id);
    }

    public async Task MarkTimeoutAsync(long id, CancellationToken ct)
    {
        const string sql = "UPDATE orders SET status = 'Timeout', timeout_at = NOW() WHERE id = $1";
        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);
        await cmd.ExecuteNonQueryAsync(ct);
        _logger.LogWarning("Order {OrderId} timed out", id);
    }

    public async Task ResetToOpenAsync(long id, CancellationToken ct)
    {
        const string sql = "UPDATE orders SET status = 'Open', started_at = NULL WHERE id = $1";
        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);
        await cmd.ExecuteNonQueryAsync(ct);
        _logger.LogInformation("Order {OrderId} reset to Open", id);
    }
}