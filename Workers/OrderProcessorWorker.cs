using event_driven_order_processor.Data;
using event_driven_order_processor.Models;
using event_driven_order_processor.Services;

namespace event_driven_order_processor.Workers;

public class OrderProcessorWorker : BackgroundService
{
    private readonly ILogger<OrderProcessorWorker> _logger;
    private readonly IMessageChannel _messageChannel;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public OrderProcessorWorker(
        ILogger<OrderProcessorWorker> logger,
        IMessageChannel messageChannel,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _messageChannel = messageChannel;
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderProcessorWorker starting");

        try
        {
            await foreach (var message in _messageChannel.ReadAsync(stoppingToken))
            {
                try
                {
                    await ProcessOrderAsync(message, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Order processing cancelled during shutdown");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing order {OrderId}", message.OrderId);
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("OrderProcessorWorker received shutdown signal");
        }

        _logger.LogInformation("OrderProcessorWorker shutting down");
    }

    private async Task ProcessOrderAsync(OrderCreatedMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing order {OrderId}", message.OrderId);

        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var order = await dbContext.Orders.FindAsync([message.OrderId], cancellationToken);
        if (order is null)
        {
            _logger.LogWarning("Order {OrderId} not found", message.OrderId);
            return;
        }

        order.Status = OrderStatus.Processed;
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Order {OrderId} processed successfully", message.OrderId);
        _logger.LogInformation(
            "Confirmation Email Sent to customer {CustomerName} for order {OrderId}",
            order.CustomerName,
            order.Id);
    }
}
