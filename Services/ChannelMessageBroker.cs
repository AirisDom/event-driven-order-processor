using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace event_driven_order_processor.Services;

public class ChannelMessageBroker : IMessageChannel
{
    private readonly Channel<OrderCreatedMessage> _channel;
    private readonly ILogger<ChannelMessageBroker> _logger;

    public ChannelMessageBroker(ILogger<ChannelMessageBroker> logger)
    {
        _logger = logger;
        _channel = Channel.CreateUnbounded<OrderCreatedMessage>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });
    }

    public async ValueTask WriteAsync(OrderCreatedMessage message, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(message, cancellationToken);
        _logger.LogDebug("Message written to channel for OrderId={OrderId}", message.OrderId);
    }

    public async IAsyncEnumerable<OrderCreatedMessage> ReadAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var message in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            _logger.LogDebug("Message read from channel for OrderId={OrderId}", message.OrderId);
            yield return message;
        }
    }
}
