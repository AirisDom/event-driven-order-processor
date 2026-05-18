using Microsoft.EntityFrameworkCore;
using event_driven_order_processor.Data;
using event_driven_order_processor.Models;
using event_driven_order_processor.Services;
using event_driven_order_processor.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=orders.db"));

builder.Services.AddSingleton<IMessageChannel, ChannelMessageBroker>();

builder.Services.AddHostedService<OrderProcessorWorker>();

builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

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

    db.Orders.Add(order);
    await db.SaveChangesAsync();

    var message = new OrderCreatedMessage(order.Id);
    await messageChannel.WriteAsync(message);
    logger.LogInformation("OrderCreated message published for OrderId: {OrderId}", order.Id);

    return Results.Accepted($"/orders/{order.Id}", new { order.Id });
});

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
});

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
});

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
