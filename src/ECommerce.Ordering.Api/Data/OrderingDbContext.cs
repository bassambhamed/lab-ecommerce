using ECommerce.Ordering.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Ordering.Api.Data;

public class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Customer).HasMaxLength(200).IsRequired();
            // Stored as text ("Confirmed"), like the JSON contract, rather than as an integer.
            entity.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            entity.Ignore(o => o.Total);
            // An item always belongs to an order: the OrderId foreign key is NOT NULL.
            entity.HasMany(o => o.Items).WithOne().IsRequired().OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("OrderItems");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(i => i.UnitPrice).HasPrecision(18, 2);
        });
    }
}
