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

    // Must stay in sync with the Customer column length in OrderingDbContext:
    // PostgreSQL enforces it, so a longer value would otherwise fail on SaveChanges with a 500.
    private const int CustomerMaxLength = 200;

    /// <summary>
    /// Validates the request before any call to Catalog. Returns an error message, or null if valid.
    /// </summary>
    private static string? Validate(CreateOrderRequest request)
    {
        // Fields can be null when absent from the JSON body, despite the non-nullable declarations.
        if (request.Customer != null && request.Customer.Trim().Length > 0 && !(request.Customer.Trim().Length > CustomerMaxLength))
        {
            if (request.Items != null && request.Items.Count > 0)
            {
                bool hasNull = false;
                foreach (var item in request.Items)
                {
                    if (item == null)
                    {
                        hasNull = true;
                    }
                }

                if (!hasNull)
                {
                    foreach (var item in request.Items)
                    {
                        if (item != null)
                        {
                            if (item.Quantity <= 0)
                            {
                                return $"Quantity for product {item.ProductId} must be greater than zero.";
                            }
                        }
                    }
                }
                else
                {
                    return "Order items must not be null.";
                }

                return null;
            }
            else
            {
                return "An order must contain at least one item.";
            }
        }
        else
        {
            if (request.Customer == null || request.Customer.Trim().Length == 0)
            {
                return "Customer is required.";
            }
            else
            {
                return $"Customer must be at most {CustomerMaxLength} characters.";
            }
        }
    }
}

public record CreateOrderRequest(string Customer, List<OrderLine> Items);
public record OrderLine(int ProductId, int Quantity);
