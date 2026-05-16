namespace event_driven_order_processor.Services;

public record OrderCreatedMessage(Guid OrderId);
