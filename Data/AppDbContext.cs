using Microsoft.EntityFrameworkCore;
using event_driven_order_processor.Models;

namespace event_driven_order_processor.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();
}
