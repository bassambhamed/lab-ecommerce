# Modèles vérifiés pour les tests d'intégration ECommerce

Ces extraits compilent et passent sur ce dépôt (.NET 10, xUnit 2.9, EF Core InMemory 10).

## Référence de paquet (dans le `.csproj` du projet de tests)

```xml
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.9" />
```

Alignez la version sur celle des paquets ASP.NET Core de l'API testée.

## Fabrique pour Ordering.Api, avec un faux Catalog

```csharp
using System.Net;
using System.Net.Http.Json;
using ECommerce.Ordering.Api.Data;
using ECommerce.Ordering.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ECommerce.Ordering.Api.Tests;

public sealed class OrderingApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"ordering-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Aspire normally injects this setting. The fake handler answers instead of the network.
        builder.UseSetting("services:catalog:http:0", "http://catalog.test");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<OrderingDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<OrderingDbContext>>();
            services.AddDbContext<OrderingDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            services.AddHttpClient<CatalogServiceClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new FakeCatalogHandler());
        });
    }
}

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
```

## Fabrique pour Catalog.Api

Même principe, sans faux service : Catalog n'appelle personne. `EnsureCreated` dans
`Program.cs` crée les produits 1 à 4 dans la base propre à la fabrique.

```csharp
public sealed class CatalogApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"catalog-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<CatalogDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CatalogDbContext>>();
            services.AddDbContext<CatalogDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
}
```

## Une classe de test : une fabrique par test

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace ECommerce.Ordering.Api.Tests;

public sealed class CreateOrderTests : IDisposable
{
    private readonly OrderingApiFactory _factory = new();
    private readonly HttpClient _client;

    public CreateOrderTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task ValidOrder_Returns201WithLocationAndTotal()
    {
        var response = await PostOrderAsync("""{"customer":"Ada","items":[{"productId":1,"quantity":2}]}""");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);
        Assert.Equal("Confirmed", order.Status);
        Assert.Equal(239.98m, order.Total);
    }

    private Task<HttpResponseMessage> PostOrderAsync(string json) =>
        _client.PostAsync("/api/orders", new StringContent(json, Encoding.UTF8, "application/json"));

    // The consumer's view of the contract: Status travels as a string.
    private sealed record OrderResponse(int Id, string Customer, string Status, decimal Total);
}
```
