namespace ScalableOrderProcessing.Models;

// Mirrors the orders table (see init-db/01-schema.sql)
public class Order
{
    public long Id { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public DateTimeOffset? TimeoutAt { get; set; }
}
