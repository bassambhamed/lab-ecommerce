using ECommerce.Ordering.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Ordering.Api.Data;

public class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<PromoCode> PromoCodes => Set<PromoCode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Customer).HasMaxLength(200).IsRequired();
            entity.Ignore(o => o.Subtotal);
            entity.Ignore(o => o.Total);
            entity.Property(o => o.DiscountAmount).HasPrecision(18, 2);
            entity.Property(o => o.AppliedPromoCode).HasMaxLength(PromoCodeMaxLength);
            entity.HasMany(o => o.Items).WithOne().OnDelete(DeleteBehavior.Cascade);
            // Shadow FK: keeps the API payload free of a redundant "IssuedPromoCodeCode" field.
            entity.HasOne(o => o.IssuedPromoCode).WithOne().HasForeignKey<Order>("IssuedPromoCodeCode");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(i => i.UnitPrice).HasPrecision(18, 2);
        });

        modelBuilder.Entity<PromoCode>(entity =>
        {
            entity.HasKey(p => p.Code);
            entity.Property(p => p.Code).HasMaxLength(PromoCodeMaxLength);
            entity.Property(p => p.RedeemedAt).IsConcurrencyToken();
        });
    }

    public const int PromoCodeMaxLength = 32;
}
