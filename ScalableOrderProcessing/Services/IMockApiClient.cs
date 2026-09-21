using ScalableOrderProcessing.Models;

namespace ScalableOrderProcessing.Services;

public interface IMockApiClient
{
    Task ProcessOrderAsync(Order order, CancellationToken ct);
}