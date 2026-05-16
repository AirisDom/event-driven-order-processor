namespace event_driven_order_processor.Services;

public interface IMessageChannel
{
    ValueTask WriteAsync(OrderCreatedMessage message, CancellationToken cancellationToken = default);
    IAsyncEnumerable<OrderCreatedMessage> ReadAsync(CancellationToken cancellationToken = default);
}
