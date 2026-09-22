using ScalableOrderProcessing.Models;

namespace ScalableOrderProcessing.Services;

public interface IOrderProcessor
{
    Task ProcessAsync(Order order, CancellationToken serviceStoppingToken);
}
