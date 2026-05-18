using System.ComponentModel.DataAnnotations;

namespace event_driven_order_processor.Models;

public record CreateOrderRequest(
    [Required(ErrorMessage = "CustomerName is required")]
    string CustomerName,
    [Required(ErrorMessage = "ProductName is required")]
    string ProductName,
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
    int Quantity);
