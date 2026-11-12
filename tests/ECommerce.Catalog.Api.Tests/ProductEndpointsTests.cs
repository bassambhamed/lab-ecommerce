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

    // PostgreSQL: seeding ids 1 to 4 explicitly must not leave the identity sequence at 1,
    // or the first created product collides with product 1.
    [Fact]
    public async Task CreateProduct_AfterSeededProducts_Returns201WithNextId()
    {
        using var body = new StringContent(
            """{"name":"Webcam","description":"1080p","price":59.90,"availableStock":10}""",
            System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/products", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var product = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(product);
        Assert.Equal(5, product.Id);
    }

    // Column limits (varchar(200), varchar(2000), numeric(18,2)) are enforced by PostgreSQL:
    // the API must reject what the database would refuse with a 400, not let it fail with a 500.
    public static TheoryData<string, string> InvalidProducts => new()
    {
        { "missing name", """{"price":10,"availableStock":1}""" },
        { "blank name", """{"name":"   ","price":10,"availableStock":1}""" },
        { "name over 200 chars", $$"""{"name":"{{new string('n', 201)}}","price":10,"availableStock":1}""" },
        { "description over 2000 chars", $$"""{"name":"Webcam","description":"{{new string('d', 2001)}}","price":10,"availableStock":1}""" },
        { "negative price", """{"name":"Webcam","price":-1,"availableStock":1}""" },
        { "price with 3 decimals", """{"name":"Webcam","price":59.999,"availableStock":1}""" },
        { "price over numeric(18,2)", """{"name":"Webcam","price":10000000000000000,"availableStock":1}""" },
        { "negative stock", """{"name":"Webcam","price":10,"availableStock":-1}""" },
    };

    [Theory]
    [MemberData(nameof(InvalidProducts))]
    public async Task CreateProduct_InvalidProduct_Returns400(string scenario, string json)
    {
        var response = await PostProductAsync(json);

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{scenario}: got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task CreateProduct_NameOfExactly200Chars_Returns201()
    {
        var response = await PostProductAsync($$"""{"name":"{{new string('n', 200)}}","price":10,"availableStock":1}""");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<HttpResponseMessage> PostProductAsync(string json)
    {
        using var body = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        return await _client.PostAsync("/api/products", body);
    }

    // The consumer's view of the contract.
    private sealed record ProductResponse(int Id, string Name, string? Description, decimal Price, int AvailableStock);
}
