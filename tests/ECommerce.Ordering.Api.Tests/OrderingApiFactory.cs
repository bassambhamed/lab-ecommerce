using System.Net;
using System.Net.Http.Json;
using ECommerce.Ordering.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Ordering.Api.Tests;

/// <summary>
/// Hosts Ordering.Api in memory against its own, brand-new PostgreSQL database and a fake Catalog:
/// no test reaches the real Catalog. Migrate() in Program.cs creates the database.
/// </summary>
public sealed class OrderingApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString = PostgresServer.NewDatabaseConnectionString("ordering_tests");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Aspire normally injects these settings. The fake handler answers instead of the network.
        builder.UseSetting("ConnectionStrings:orderingdb", _connectionString);
        builder.UseSetting("services:catalog:http:0", "http://catalog.test");

        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient<CatalogServiceClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new FakeCatalogHandler());
        });
    }
}

/// <summary>
/// Answers GET /api/products/{id} like Catalog does: 200 with the product for ids 1 and 2, 404 otherwise.
/// </summary>
public sealed class FakeCatalogHandler : HttpMessageHandler
{
    private const string ProductsPath = "/api/products/";

    private static readonly Dictionary<int, CatalogProduct> Products = new()
    {
        [1] = new CatalogProduct(1, "Mechanical Keyboard", "Hot-swappable RGB keyboard", 119.99m, 50),
        [2] = new CatalogProduct(2, "Wireless Mouse", "Ergonomic 8k DPI mouse", 49.50m, 120),
    };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;

        if (request.Method == HttpMethod.Get
            && path.StartsWith(ProductsPath, StringComparison.Ordinal)
            && int.TryParse(path[ProductsPath.Length..], out var id)
            && Products.TryGetValue(id, out var product))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(product) });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
