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

    // Scenario 4
    [Fact]
    public async Task CreateOrder_ValidOrder_Returns201WithLocationConfirmedStatusAndTotal()
    {
        var response = await PostOrderAsync("""{"customer":"Ada","items":[{"productId":1,"quantity":2}]}""");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);
        Assert.Equal("Confirmed", order.Status);
        Assert.Equal(239.98m, order.Total);

        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"/api/orders/{order.Id}", response.Headers.Location.ToString());
    }

    // Scenario 5
    [Fact]
    public async Task CreateOrder_EmptyCart_Returns400()
    {
        var response = await PostOrderAsync("""{"customer":"Ada","items":[]}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Scenario 6
    [Fact]
    public async Task CreateOrder_UnknownProduct_Returns400NamingTheProduct()
    {
        var response = await PostOrderAsync("""{"customer":"Ada","items":[{"productId":99,"quantity":1}]}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var message = await response.Content.ReadFromJsonAsync<string>();
        Assert.NotNull(message);
        Assert.Contains("Product 99", message);
    }

    // Scenario 7
    [Fact]
    public async Task CreateOrder_EmptyCustomer_Returns400()
    {
        var response = await PostOrderAsync("""{"customer":"","items":[{"productId":1,"quantity":1}]}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Scenario 8
    [Fact]
    public async Task CreateOrder_ZeroQuantity_Returns400()
    {
        var response = await PostOrderAsync("""{"customer":"Ada","items":[{"productId":1,"quantity":0}]}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Scenario 9: bug reported by the customer (500 instead of 400 when "items" is absent).
    [Fact]
    public async Task CreateOrder_MissingItemsProperty_Returns400()
    {
        var response = await PostOrderAsync("""{"customer":"Ada"}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // The exact JSON a client sends: the only way to test absent or mistyped properties.
    private Task<HttpResponseMessage> PostOrderAsync(string json) =>
        _client.PostAsync("/api/orders", new StringContent(json, Encoding.UTF8, "application/json"));

    // The consumer's view of the contract: Status travels as a string.
    private sealed record OrderResponse(int Id, string Customer, string Status, decimal Total);
}
