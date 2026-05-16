namespace event_driven_order_processor.Models;

public record CreateOrderRequest(string CustomerName, string ProductName, int Quantity);
