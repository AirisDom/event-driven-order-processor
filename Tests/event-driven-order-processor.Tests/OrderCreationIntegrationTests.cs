using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using event_driven_order_processor.Data;

namespace event_driven_order_processor.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        });

        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection?.Dispose();
        }
    }
}

public class OrderCreationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OrderCreationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateOrder_ReturnsAccepted_AndEventuallyBecomesProcessed()
    {
        var client = _factory.CreateClient();

        var orderRequest = new
        {
            CustomerName = "Test Customer",
            ProductName = "Test Product",
            Quantity = 5
        };

        var response = await client.PostAsJsonAsync("/orders", orderRequest);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var createResponse = await response.Content.ReadFromJsonAsync<CreateOrderResponse>();
        Assert.NotNull(createResponse);
        Assert.NotEqual(Guid.Empty, createResponse.Id);

        var orderId = createResponse.Id;
        var maxAttempts = 50;
        var delayMs = 100;
        var processed = false;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var getResponse = await client.GetAsync($"/orders/{orderId}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            var order = await getResponse.Content.ReadFromJsonAsync<OrderResponse>();
            Assert.NotNull(order);

            if (order.Status == "Processed")
            {
                processed = true;
                Assert.Equal("Test Customer", order.CustomerName);
                Assert.Equal("Test Product", order.ProductName);
                Assert.Equal(5, order.Quantity);
                break;
            }

            await Task.Delay(delayMs);
        }

        Assert.True(processed, "Order did not reach Processed status within the timeout period");
    }

    [Fact]
    public async Task CreateOrder_WithInvalidData_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var invalidRequest = new
        {
            CustomerName = "",
            ProductName = "",
            Quantity = 0
        };

        var response = await client.PostAsJsonAsync("/orders", invalidRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetOrder_WithNonExistentId_ReturnsNotFound()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private record CreateOrderResponse(Guid Id);

    private record OrderResponse(
        Guid Id,
        string CustomerName,
        string ProductName,
        int Quantity,
        string Status,
        DateTime CreatedAt);
}
