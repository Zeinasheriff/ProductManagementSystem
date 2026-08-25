using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ProductManagement.Application.DTOs;
using ProductManagement.IntegrationTests.Infra;

namespace ProductManagement.IntegrationTests;

public class OrdersApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OrdersApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Creates a product via the admin API and returns its id.</summary>
    private async Task<int> CreateProductAsync(string name, decimal price, int stock)
    {
        var admin = await _factory.CreateAdminClientAsync();
        var response = await admin.PostAsJsonAsync("api/products",
            new { name, description = "order test", price, stockQuantity = stock });
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetInt32();
    }

    private static CreateOrderRequest Order(int productId, int qty) =>
        new(new List<CreateOrderItemRequest> { new(productId, qty) });

    [Fact]
    public async Task Create_AsAnonymous_Returns401()
    {
        var productId = await CreateProductAsync($"OrdAnon_{Guid.NewGuid():N}", 10m, 5);

        var response = await _factory.CreateClient().PostAsJsonAsync("api/orders", Order(productId, 1));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsUser_Returns201_WithServerCalculatedTotal()
    {
        var productId = await CreateProductAsync($"OrdOk_{Guid.NewGuid():N}", 15.50m, 10);

        var user = await _factory.CreateUserClientAsync();
        var response = await user.PostAsJsonAsync("api/orders", Order(productId, 3));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(46.5m, doc.RootElement.GetProperty("totalAmount").GetDecimal()); // server-side
        Assert.Equal(1, doc.RootElement.GetProperty("items").GetArrayLength());

        // CreatedAtAction must expose a location header.
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task Create_AsUser_ReducesStock_VerifiableViaPublicEndpoint()
    {
        var productId = await CreateProductAsync($"OrdStock_{Guid.NewGuid():N}", 4m, 6);

        var user = await _factory.CreateUserClientAsync();
        (await user.PostAsJsonAsync("api/orders", Order(productId, 2))).EnsureSuccessStatusCode();

        var get = await _factory.CreateClient().GetAsync($"api/products/{productId}");
        get.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());

        Assert.Equal(4, doc.RootElement.GetProperty("stockQuantity").GetInt32());
    }

    [Fact]
    public async Task Create_WithInsufficientStock_Returns400_WithProblemDetailsMessage()
    {
        var productId = await CreateProductAsync($"OrdLow_{Guid.NewGuid():N}", 9m, 1);

        var user = await _factory.CreateUserClientAsync();
        var response = await user.PostAsJsonAsync("api/orders", Order(productId, 5));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("Insufficient stock", text);
    }

    [Fact]
    public async Task Create_WithEmptyItems_Returns400_FromValidationPipeline()
    {
        var user = await _factory.CreateUserClientAsync();
        var response = await user.PostAsJsonAsync("api/orders", new { items = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetMyOrders_AsUser_IncludesPreviouslyCreatedOrder()
    {
        var productId = await CreateProductAsync($"OrdList_{Guid.NewGuid():N}", 20m, 5);

        var user = await _factory.CreateUserClientAsync();
        var create = await user.PostAsJsonAsync("api/orders", Order(productId, 1));
        create.EnsureSuccessStatusCode();

        var list = await user.GetFromJsonAsync<List<OrderDto>>("api/orders");

        Assert.NotNull(list);
        Assert.Contains(list!, o => o.Items.Any(i => i.ProductId == productId));
    }

    [Fact]
    public async Task GetById_Anonymous_Returns401()
    {
        var productId = await CreateProductAsync($"OrdPrivAnon_{Guid.NewGuid():N}", 11m, 5);

        var user = await _factory.CreateUserClientAsync();
        var create = await user.PostAsJsonAsync("api/orders", Order(productId, 1));
        create.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var orderId = doc.RootElement.GetProperty("id").GetInt32();

        var response = await _factory.CreateClient().GetAsync($"api/orders/{orderId}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_AsAdmin_ForAnyUsersOrder_Returns200()
    {
        var productId = await CreateProductAsync($"OrdAdm_{Guid.NewGuid():N}", 13m, 5);

        var user = await _factory.CreateUserClientAsync();
        var create = await user.PostAsJsonAsync("api/orders", Order(productId, 2));
        create.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var orderId = doc.RootElement.GetProperty("id").GetInt32();

        var admin = await _factory.CreateAdminClientAsync();
        var response = await admin.GetAsync($"api/orders/{orderId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(26m, body.RootElement.GetProperty("totalAmount").GetDecimal());
    }
}