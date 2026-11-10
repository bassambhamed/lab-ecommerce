namespace ECommerce.Web.Services;

/// <summary>
/// Talks to the Ordering service through the gateway ("/ordering/...").
/// Base address is resolved by Aspire service discovery.
/// </summary>
public class OrderingApiClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<OrderDto>> GetOrdersAsync(CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<List<OrderDto>>("/ordering/api/orders", ct) ?? [];

    /// <summary>Returns the created order (with its issued promo code), or an error message.</summary>
    public async Task<(OrderDto? Order, string? Error)> CreateOrderAsync(CreateOrderDto order, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync("/ordering/api/orders", order, ct);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<OrderDto>(ct), null);
        }

        var detail = await response.Content.ReadAsStringAsync(ct);
        return (null, string.IsNullOrWhiteSpace(detail) ? response.ReasonPhrase : detail.Trim('"'));
    }
}

public record OrderDto(
    int Id, string Customer, DateTimeOffset CreatedAt, string Status,
    decimal Subtotal, decimal DiscountAmount, decimal Total, string? AppliedPromoCode,
    PromoCodeDto? IssuedPromoCode, List<OrderItemDto> Items);
public record PromoCodeDto(string Code, int DiscountPercent, DateTimeOffset ExpiresAt, DateTimeOffset? RedeemedAt);
public record OrderItemDto(int ProductId, string ProductName, decimal UnitPrice, int Quantity);
public record CreateOrderDto(string Customer, List<OrderLineDto> Items, string? PromoCode = null);
public record OrderLineDto(int ProductId, int Quantity);
