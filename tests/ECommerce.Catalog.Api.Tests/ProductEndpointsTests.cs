using System.Net;
using System.Net.Http.Json;

namespace ECommerce.Catalog.Api.Tests;

public sealed class ProductEndpointsTests : IDisposable
{
    private readonly CatalogApiFactory _factory = new();
    private readonly HttpClient _client;

    public ProductEndpointsTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // Scenario 1
    [Fact]
    public async Task GetProducts_SeededCatalog_Returns200WithFourProducts()
    {
        var response = await _client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var products = await response.Content.ReadFromJsonAsync<List<ProductResponse>>();
        Assert.NotNull(products);
        Assert.Equal(4, products.Count);
    }

    // Scenario 2
    [Fact]
    public async Task GetProductById_ExistingProduct_Returns200WithNameAndPrice()
    {
        var response = await _client.GetAsync("/api/products/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var product = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(product);
        Assert.Equal(1, product.Id);
        Assert.Equal("Mechanical Keyboard", product.Name);
        Assert.Equal(119.99m, product.Price);
    }

    // Scenario 3
    [Fact]
    public async Task GetProductById_UnknownProduct_Returns404()
    {
        var response = await _client.GetAsync("/api/products/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // The consumer's view of the contract.
    private sealed record ProductResponse(int Id, string Name, string? Description, decimal Price, int AvailableStock);
}
