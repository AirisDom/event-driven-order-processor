namespace event_driven_order_processor.Workers;

public class OrderProcessorWorker : BackgroundService
{
    private readonly ILogger<OrderProcessorWorker> _logger;

    public OrderProcessorWorker(ILogger<OrderProcessorWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderProcessorWorker starting");

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }

        _logger.LogInformation("OrderProcessorWorker shutting down");
    }
}
