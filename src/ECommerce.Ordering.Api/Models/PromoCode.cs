namespace ECommerce.Ordering.Api.Models;

/// <summary>
/// Single-use discount code issued with every confirmed order and redeemable on a later order.
/// </summary>
public class PromoCode
{
    public required string Code { get; set; }
    public int DiscountPercent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Set when the code is used. Also a concurrency token, so a code can't be redeemed twice.</summary>
    public DateTimeOffset? RedeemedAt { get; set; }
}
