using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using event_driven_order_processor.Data;
using event_driven_order_processor.Models;
using event_driven_order_processor.Services;
using event_driven_order_processor.Workers;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=orders.db";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddSingleton<IMessageChannel, ChannelMessageBroker>();

builder.Services.AddHostedService<OrderProcessorWorker>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Event-Driven Order Processor API",
        Version = "v1",
        Description = "A scalable order processing API where orders are queued for background processing"
    });
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Event-Driven Order Processor API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health");

app.MapPost("/orders", async (
    CreateOrderRequest request,
    AppDbContext db,
    IMessageChannel messageChannel,
    ILogger<Program> logger) =>
{
    var validationErrors = new List<string>();

    if (string.IsNullOrWhiteSpace(request.CustomerName))
    {
        validationErrors.Add("CustomerName is required");
    }

    if (string.IsNullOrWhiteSpace(request.ProductName))
    {
        validationErrors.Add("ProductName is required");
    }

    if (request.Quantity <= 0)
    {
        validationErrors.Add("Quantity must be greater than 0");
    }

    if (validationErrors.Count > 0)
    {
        return Results.BadRequest(new { errors = validationErrors });
    }

    var order = new Order
    {
        Id = Guid.NewGuid(),
        CustomerName = request.CustomerName,
        ProductName = request.ProductName,
        Quantity = request.Quantity,
        Status = OrderStatus.Pending,
        CreatedAt = DateTime.UtcNow
    };

    using (logger.BeginScope(new Dictionary<string, object>
    {
        ["OrderId"] = order.Id,
        ["CustomerName"] = order.CustomerName
    }))
    {
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Order created with Status={Status}, ProductName={ProductName}, Quantity={Quantity}",
            order.Status,
            order.ProductName,
            order.Quantity);

        var message = new OrderCreatedMessage(order.Id);
        await messageChannel.WriteAsync(message);

        logger.LogInformation("OrderCreated message published to queue");
    }

    return Results.Accepted($"/orders/{order.Id}", new { order.Id });
})
.WithName("CreateOrder")
.WithSummary("Create a new order")
.WithDescription("Creates a new order with Pending status and queues it for background processing. Returns immediately with 202 Accepted.")
.Produces<object>(StatusCodes.Status202Accepted)
.Produces<object>(StatusCodes.Status400BadRequest)
.WithTags("Orders");

app.MapGet("/orders", async (string? status, AppDbContext db) =>
{
    IQueryable<Order> query = db.Orders;

    if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsedStatus))
    {
        query = query.Where(o => o.Status == parsedStatus);
    }

    var orders = await query
        .OrderByDescending(o => o.CreatedAt)
        .Select(o => new
        {
            o.Id,
            o.CustomerName,
            o.ProductName,
            o.Quantity,
            Status = o.Status.ToString(),
            o.CreatedAt
        })
        .ToListAsync();

    return Results.Ok(orders);
})
.WithName("GetAllOrders")
.WithSummary("Get all orders")
.WithDescription("Retrieves all orders, optionally filtered by status (Pending or Processed). Results are sorted by creation date descending.")
.Produces<object[]>(StatusCodes.Status200OK)
.WithTags("Orders");

app.MapGet("/orders/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var order = await db.Orders.FindAsync(id);
    if (order is null)
    {
        return Results.NotFound(new { message = $"Order with ID {id} not found" });
    }

    return Results.Ok(new
    {
        order.Id,
        order.CustomerName,
        order.ProductName,
        order.Quantity,
        Status = order.Status.ToString(),
        order.CreatedAt
    });
})
.WithName("GetOrderById")
.WithSummary("Get an order by ID")
.WithDescription("Retrieves a specific order by its unique identifier. Use this to check the processing status of an order.")
.Produces<object>(StatusCodes.Status200OK)
.Produces<object>(StatusCodes.Status404NotFound)
.WithTags("Orders");

app.Run();

public partial class Program { }
