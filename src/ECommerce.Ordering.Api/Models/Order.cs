namespace ECommerce.Ordering.Api.Models;

public class Order
{
    public int Id { get; set; }
    public required string Customer { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public List<OrderItem> Items { get; set; } = [];

    /// <summary>Promo code redeemed on this order, if any.</summary>
    public string? AppliedPromoCode { get; set; }

    /// <summary>Amount taken off the subtotal by <see cref="AppliedPromoCode"/>.</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>Promo code issued to the customer when this order was confirmed.</summary>
    public PromoCode? IssuedPromoCode { get; set; }

    public decimal Subtotal => Items.Sum(i => i.UnitPrice * i.Quantity);
    public decimal Total => Subtotal - DiscountAmount;
}

public class OrderItem
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public required string ProductName { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public enum OrderStatus
{
    Pending,
    Confirmed,
    Shipped,
    Cancelled
}
