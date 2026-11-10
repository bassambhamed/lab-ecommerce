using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using ECommerce.Ordering.Api.Data;
using ECommerce.Ordering.Api.Models;
using Microsoft.Extensions.Options;

namespace ECommerce.Ordering.Api.Services;

/// <summary>Bound from the "PromoCodes" configuration section.</summary>
public class PromoCodeOptions
{
    public const string SectionName = "PromoCodes";

    [Range(1, 100)]
    public int DiscountPercent { get; set; } = 10;

    [Range(1, 3650)]
    public int ValidityDays { get; set; } = 30;
}

/// <summary>
/// Issues and checks promo codes. Changes are only tracked on the DbContext:
/// the caller saves them together with the order.
/// </summary>
public class PromoCodeService(OrderingDbContext db, TimeProvider time, IOptions<PromoCodeOptions> options)
{
    private const string Prefix = "PROMO-";
    // No 0/O or 1/I, to keep codes easy to read and type.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Normalize(string code) => code.Trim().ToUpperInvariant();

    /// <summary>Returns the code if it can be redeemed now, otherwise an error message.</summary>
    public async Task<(PromoCode? PromoCode, string? Error)> FindRedeemableAsync(string code, CancellationToken ct)
    {
        var normalized = Normalize(code);
        var promo = await db.PromoCodes.FindAsync([normalized], ct);

        if (promo is null)
        {
            return (null, $"Promo code {normalized} does not exist.");
        }

        if (promo.RedeemedAt is not null)
        {
            return (null, $"Promo code {normalized} has already been used.");
        }

        if (promo.ExpiresAt <= time.GetUtcNow())
        {
            return (null, $"Promo code {normalized} expired on {promo.ExpiresAt:yyyy-MM-dd}.");
        }

        return (promo, null);
    }

    /// <summary>Applies the discount to the order and marks the code as used.</summary>
    public void Redeem(PromoCode promo, Order order)
    {
        order.AppliedPromoCode = promo.Code;
        order.DiscountAmount = Math.Round(order.Subtotal * promo.DiscountPercent / 100m, 2, MidpointRounding.AwayFromZero);
        promo.RedeemedAt = time.GetUtcNow();
    }

    /// <summary>Creates a new, unique code valid for the configured number of days.</summary>
    public async Task<PromoCode> IssueAsync(CancellationToken ct)
    {
        string code;
        do
        {
            code = Prefix + RandomNumberGenerator.GetString(Alphabet, 8);
        }
        while (await db.PromoCodes.FindAsync([code], ct) is not null);

        var now = time.GetUtcNow();
        var promo = new PromoCode
        {
            Code = code,
            DiscountPercent = options.Value.DiscountPercent,
            CreatedAt = now,
            ExpiresAt = now.AddDays(options.Value.ValidityDays)
        };

        db.PromoCodes.Add(promo);
        return promo;
    }
}
