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
            CancellationToken ct) =>
        {
            if (Validate(request) is { } error)
            {
                return Results.BadRequest(error);
            }

            var order = new Order
            {
                Customer = request.Customer.Trim(),
                CreatedAt = DateTimeOffset.UtcNow,
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

            order.Status = OrderStatus.Confirmed;
            db.Orders.Add(order);
            await db.SaveChangesAsync(ct);

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

public record CreateOrderRequest(string Customer, List<OrderLine> Items);
public record OrderLine(int ProductId, int Quantity);
