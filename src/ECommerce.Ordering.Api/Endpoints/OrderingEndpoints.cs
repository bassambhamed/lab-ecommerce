using ECommerce.Ordering.Api.Data;
using ECommerce.Ordering.Api.Models;
using ECommerce.Ordering.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Ordering.Api.Endpoints;

public static class OrderingEndpoints
{
    public static RouteGroupBuilder MapOrderingEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/orders").WithTags("Ordering");

        // Issued promo codes are deliberately not loaded here: they are bearer codes and these
        // endpoints are public. Only the POST response hands a code to the customer who ordered.
        group.MapGet("/", async (OrderingDbContext db) =>
            await db.Orders.AsNoTracking().Include(o => o.Items).ToListAsync())
            .WithName("GetOrders");

        group.MapGet("/{id:int}", async (int id, OrderingDbContext db) =>
            await db.Orders.AsNoTracking().Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id) is { } order
                ? Results.Ok(order)
                : Results.NotFound())
            .WithName("GetOrderById");

        group.MapPost("/", async (
            CreateOrderRequest request,
            OrderingDbContext db,
            CatalogServiceClient catalog,
            PromoCodeService promoCodes,
            TimeProvider time,
            CancellationToken ct) =>
        {
            if (Validate(request) is { } error)
            {
                return Results.BadRequest(error);
            }

            // Check the promo code before calling Catalog: it only needs the local database.
            PromoCode? promo = null;
            if (!string.IsNullOrWhiteSpace(request.PromoCode))
            {
                (promo, var promoError) = await promoCodes.FindRedeemableAsync(request.PromoCode, ct);
                if (promoError is not null)
                {
                    return Results.BadRequest(promoError);
                }
            }

            var order = new Order
            {
                Customer = request.Customer.Trim(),
                CreatedAt = time.GetUtcNow(),
                Status = OrderStatus.Pending
            };

            // Validate each product against the Catalog service (sync HTTP call).
            foreach (var item in request.Items)
            {
                var product = await catalog.GetProductAsync(item.ProductId, ct);
                if (product is null)
                {
                    return Results.BadRequest($"Product {item.ProductId} does not exist.");
                }

                order.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = product.Price,
                    Quantity = item.Quantity
                });
            }

            if (promo is not null)
            {
                promoCodes.Redeem(promo, order);
            }

            order.Status = OrderStatus.Confirmed;
            order.IssuedPromoCode = await promoCodes.IssueAsync(ct);
            db.Orders.Add(order);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException) when (promo is not null)
            {
                // Another order redeemed the same code between our check and this save.
                return Results.BadRequest($"Promo code {promo.Code} has already been used.");
            }

            return Results.CreatedAtRoute("GetOrderById", new { id = order.Id }, order);
        })
        .WithName("CreateOrder");

        return group;
    }

    // Must stay in sync with the Customer column length in OrderingDbContext
    // (not enforced by the in-memory provider).
    private const int CustomerMaxLength = 200;

    /// <summary>
    /// Validates the request before any call to Catalog. Returns an error message, or null if valid.
    /// </summary>
    private static string? Validate(CreateOrderRequest request)
    {
        // Fields can be null when absent from the JSON body, despite the non-nullable declarations.
        if (string.IsNullOrWhiteSpace(request.Customer))
        {
            return "Customer is required.";
        }

        if (request.Customer.Trim().Length > CustomerMaxLength)
        {
            return $"Customer must be at most {CustomerMaxLength} characters.";
        }

        if (request.PromoCode is { } code && code.Trim().Length > OrderingDbContext.PromoCodeMaxLength)
        {
            return $"Promo code must be at most {OrderingDbContext.PromoCodeMaxLength} characters.";
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return "An order must contain at least one item.";
        }

        if (request.Items.Any(i => i is null))
        {
            return "Order items must not be null.";
        }

        if (request.Items.FirstOrDefault(i => i.Quantity <= 0) is { } invalid)
        {
            return $"Quantity for product {invalid.ProductId} must be greater than zero.";
        }

        return null;
    }
}

public record CreateOrderRequest(string Customer, List<OrderLine> Items, string? PromoCode = null);
public record OrderLine(int ProductId, int Quantity);
