using System.Threading.Channels;

namespace event_driven_order_processor.Services;

public class ChannelMessageBroker : IMessageChannel
{
    private readonly Channel<OrderCreatedMessage> _channel;

    public ChannelMessageBroker()
    {
        _channel = Channel.CreateUnbounded<OrderCreatedMessage>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });
    }

    public ValueTask WriteAsync(OrderCreatedMessage message, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(message, cancellationToken);
    }

    public async IAsyncEnumerable<OrderCreatedMessage> ReadAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var message in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return message;
        }
    }
}
