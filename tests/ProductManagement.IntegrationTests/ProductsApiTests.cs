using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProductManagement.Domain.Entities;
using ProductManagement.Infrastructure.Persistence;
using ProductManagement.IntegrationTests.Infra;

namespace ProductManagement.IntegrationTests;

public class ProductsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ProductsApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task SeedAsync(params Product[] products)
    {
        await _factory.EnsureSeededAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Products.AddRange(products);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Search_Anonymous_Returns200_WithPagedResult()
    {
        await SeedAsync(new Product { Name = $"AnonSearch_{Guid.NewGuid():N}", Price = 5m, StockQuantity = 1 });

        var client = _factory.CreateClient();
        var response = await client.GetAsync("api/products/search?pageNumber=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("items", out var items));
        Assert.True(root.TryGetProperty("totalCount", out var totalCount));
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        Assert.True(totalCount.GetInt32() > 0);
    }

    [Fact]
    public async Task GetById_Anonymous_Returns200()
    {
        await SeedAsync(new Product { Name = $"GetById_{Guid.NewGuid():N}", Price = 7m, StockQuantity = 2 });

        int id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            id = await db.Products.AsNoTracking()
                .Where(p => p.Name!.StartsWith("GetById_"))
                .Select(p => p.Id).SingleAsync();
        }

        var response = await _factory.CreateClient().GetAsync($"api/products/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsAnonymous_Returns401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("api/products",
            new { name = "Nope", description = "", price = 1m, stockQuantity = 1 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsStandardUser_Returns403()
    {
        var client = await _factory.CreateUserClientAsync();

        var response = await client.PostAsJsonAsync("api/products",
            new { name = $"Forbidden_{Guid.NewGuid():N}", description = "", price = 1m, stockQuantity = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsAdmin_Returns201_AndProductAppearsInSearch()
    {
        var client = await _factory.CreateAdminClientAsync();
        var name = $"AdminCreated_{Guid.NewGuid():N}";

        var createResponse = await client.PostAsJsonAsync("api/products",
            new { name, description = "via admin", price = 42.5m, stockQuantity = 9 });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var doc = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("id").GetInt32() > 0);

        // Verify through the public search endpoint.
        var search = await client.GetAsync($"api/products/search?name={Uri.EscapeDataString(name)}");
        search.EnsureSuccessStatusCode();
        using var sdoc = JsonDocument.Parse(await search.Content.ReadAsStringAsync());
        Assert.Equal(1, sdoc.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Update_AsAdmin_ChangesFields()
    {
        var client = await _factory.CreateAdminClientAsync();
        var name = $"ToUpdate_{Guid.NewGuid():N}";

        var created = await client.PostAsJsonAsync("api/products",
            new { name, description = "", price = 10m, stockQuantity = 3 });
        created.EnsureSuccessStatusCode();

        int id;
        using (var cdoc = JsonDocument.Parse(await created.Content.ReadAsStringAsync()))
        {
            id = cdoc.RootElement.GetProperty("id").GetInt32();
        }

        var update = await client.PutAsJsonAsync($"api/products/{id}",
            new { name = name + "_v2", description = "updated", price = 12m, stockQuantity = 8, isActive = true });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        using var udoc = JsonDocument.Parse(await update.Content.ReadAsStringAsync());
        Assert.Equal(12m, udoc.RootElement.GetProperty("price").GetDecimal());
        Assert.Equal(8, udoc.RootElement.GetProperty("stockQuantity").GetInt32());
    }

    [Fact]
    public async Task Deactivate_AsAdmin_SetsIsActiveFalse()
    {
        var client = await _factory.CreateAdminClientAsync();
        var name = $"ToDeactivate_{Guid.NewGuid():N}";

        var created = await client.PostAsJsonAsync("api/products",
            new { name, description = "", price = 5m, stockQuantity = 1 });
        created.EnsureSuccessStatusCode();

        int id;
        using (var cdoc = JsonDocument.Parse(await created.Content.ReadAsStringAsync()))
        {
            id = cdoc.RootElement.GetProperty("id").GetInt32();
        }

        var delete = await client.DeleteAsync($"api/products/{id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var get = await client.GetAsync($"api/products/{id}");
        get.EnsureSuccessStatusCode();
        using var gdoc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.False(gdoc.RootElement.GetProperty("isActive").GetBoolean());
    }
}