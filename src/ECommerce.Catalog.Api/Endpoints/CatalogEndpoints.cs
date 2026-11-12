using ECommerce.Catalog.Api.Data;
using ECommerce.Catalog.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Catalog.Api.Endpoints;

public static class CatalogEndpoints
{
    public static RouteGroupBuilder MapCatalogEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/products").WithTags("Catalog");

        group.MapGet("/", async (CatalogDbContext db) =>
            await db.Products.AsNoTracking().ToListAsync())
            .WithName("GetProducts");

        group.MapGet("/{id:int}", async (int id, CatalogDbContext db) =>
            await db.Products.FindAsync(id) is { } product
                ? Results.Ok(product)
                : Results.NotFound())
            .WithName("GetProductById");

        group.MapPost("/", async (CreateProductRequest request, CatalogDbContext db) =>
        {
            if (Validate(request) is { } error)
            {
                return Results.BadRequest(error);
            }

            var product = new Product
            {
                Name = request.Name.Trim(),
                Description = request.Description,
                Price = request.Price,
                AvailableStock = request.AvailableStock
            };

            db.Products.Add(product);
            await db.SaveChangesAsync();

            return Results.CreatedAtRoute("GetProductById", new { id = product.Id }, product);
        })
        .WithName("CreateProduct");

        return group;
    }

    // Must stay in sync with the column definitions in CatalogDbContext: PostgreSQL enforces them,
    // so a value beyond these limits would otherwise fail on SaveChanges with a 500.
    private const int NameMaxLength = 200;
    private const int DescriptionMaxLength = 2000;
    private const int PriceScale = 2;
    private const decimal PriceUpperBound = 10_000_000_000_000_000m; // numeric(18,2): 16 integer digits.

    /// <summary>Validates the request before saving. Returns an error message, or null if valid.</summary>
    private static string? Validate(CreateProductRequest request)
    {
        // Name can be null when absent from the JSON body, despite the non-nullable declaration.
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Name is required.";
        }

        if (request.Name.Trim().Length > NameMaxLength)
        {
            return $"Name must be at most {NameMaxLength} characters.";
        }

        if (request.Description is { Length: > DescriptionMaxLength })
        {
            return $"Description must be at most {DescriptionMaxLength} characters.";
        }

        if (request.Price < 0 || request.Price >= PriceUpperBound)
        {
            return $"Price must be between 0 and {PriceUpperBound - 0.01m}.";
        }

        if (decimal.Round(request.Price, PriceScale) != request.Price)
        {
            return $"Price must have at most {PriceScale} decimal places.";
        }

        if (request.AvailableStock < 0)
        {
            return "Available stock must not be negative.";
        }

        return null;
    }
}

public record CreateProductRequest(string Name, string? Description, decimal Price, int AvailableStock);
