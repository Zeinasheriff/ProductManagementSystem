using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.DTOs;
using ProductManagement.Application.Services;
using ProductManagement.Domain.Entities;
using ProductManagement.Infrastructure.Persistence;
using ProductManagement.UnitTests.Helpers;

namespace ProductManagement.UnitTests.Services;

public class ProductServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ProductService _sut;

    public ProductServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _sut = new ProductService(_context, NullLogger<ProductService>.Instance);
    }

    public void Dispose() => _context.Dispose();

    private void Seed(params Product[] products)
    {
        _context.Products.AddRange(products);
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    // ---------- GetProductsAsync ----------

    [Fact]
    public async Task GetProductsAsync_ReturnsAllProducts_Paged()
    {
        Seed(
            new Product { Name = "Laptop", Price = 10m, StockQuantity = 5 },
            new Product { Name = "Keyboard", Price = 5m, StockQuantity = 50 });

        var result = await _sut.GetProductsAsync(new ProductSearchRequest(null));

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.PageNumber);
    }

    [Fact]
    public async Task GetProductsAsync_FiltersByName_CaseInsensitive()
    {
        Seed(
            new Product { Name = "Gaming Laptop", Price = 10m },
            new Product { Name = "Mechanical Keyboard", Price = 10m });

        var result = await _sut.GetProductsAsync(new ProductSearchRequest("LAPTOP"));

        Assert.Equal(1, result.TotalCount);
        Assert.All(result.Items, p => Assert.Contains("Laptop", p.Name));
    }

    [Fact]
    public async Task GetProductsAsync_EscapesLikeWildcards_InSearchTerm()
    {
        Seed(
            new Product { Name = "Half Price Bundle", Price = 10m },
            new Product { Name = "50% Discount Pack", Price = 10m });

        // Wildcard characters must be matched literally, never expanded.
        // (Provider note: SQL Server honours the '\' escape char exactly;
        // the InMemory provider approximates LIKE, so we assert the
        // provider-neutral invariant: only names containing the raw token.)
        var percentResults = await _sut.GetProductsAsync(new ProductSearchRequest("%"));
        Assert.All(percentResults.Items, p => Assert.Contains("%", p.Name));

        var underscoreResults = await _sut.GetProductsAsync(new ProductSearchRequest("_"));
        Assert.All(underscoreResults.Items, p => Assert.Contains("_", p.Name));

        var literalResults = await _sut.GetProductsAsync(new ProductSearchRequest("50%"));
        Assert.Equal(1, literalResults.TotalCount);
        Assert.Contains(literalResults.Items, p => p.Name == "50% Discount Pack");
    }

    [Fact]
    public async Task GetProductsAsync_ClampsInvalidPagingValues()
    {
        Seed(
            new Product { Name = "A" }, new Product { Name = "B" }, new Product { Name = "C" });

        var result = await _sut.GetProductsAsync(new ProductSearchRequest(null, PageNumber: -5, PageSize: 9999));

        Assert.Equal(1, result.PageNumber);
        Assert.Equal(ProductService.MaxPageSize, result.PageSize);
        Assert.Equal(3, result.Items.Count);
    }

    // ---------- GetProductByIdAsync ----------

    [Fact]
    public async Task GetProductByIdAsync_ReturnsProduct_WhenItExists()
    {
        Seed(new Product { Name = "Monitor", Price = 199.99m, StockQuantity = 7 });
        var id = _context.Products.AsNoTracking().Single(p => p.Name == "Monitor").Id;

        var dto = await _sut.GetProductByIdAsync(id);

        Assert.Equal("Monitor", dto.Name);
        Assert.Equal(199.99m, dto.Price);
    }

    [Fact]
    public async Task GetProductByIdAsync_ThrowsNotFound_WhenMissing()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetProductByIdAsync(1234));
    }

    // ---------- CreateProductAsync ----------

    [Fact]
    public async Task CreateProductAsync_AddsActiveProduct_AndReturnsDto()
    {
        var request = new CreateProductRequest("Webcam", "1080p webcam", 59.99m, 12);

        var dto = await _sut.CreateProductAsync(request);

        Assert.True(dto.Id > 0);
        Assert.True(dto.IsActive);

        var stored = await _context.Products.FindAsync(dto.Id);
        Assert.NotNull(stored);
        Assert.Equal("Webcam", stored!.Name);
        Assert.Equal(12, stored.StockQuantity);
    }

    [Fact]
    public async Task CreateProductAsync_RejectsDuplicateName_IgnoringCase()
    {
        Seed(new Product { Name = "Mousepad", Price = 9.99m });

        var request = new CreateProductRequest("MOUSEPAD", "duplicate attempt", 19.99m, 1);

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateProductAsync(request));
    }

    // ---------- UpdateProductAsync ----------

    [Fact]
    public async Task UpdateProductAsync_AppliesAllFields_AndSetsUpdatedAt()
    {
        Seed(new Product { Name = "Old Name", Price = 1m, StockQuantity = 1, IsActive = true });
        var id = _context.Products.AsNoTracking().Single(p => p.Name == "Old Name").Id;

        var request = new UpdateProductRequest("New Name", "Updated", 2.5m, 20, IsActive: false);

        var dto = await _sut.UpdateProductAsync(id, request);

        Assert.Equal("New Name", dto.Name);
        Assert.False(dto.IsActive);
        Assert.Equal(20, dto.StockQuantity);
        Assert.NotNull(dto.UpdatedAt);
    }

    [Fact]
    public async Task UpdateProductAsync_ThrowsNotFound_WhenMissing()
    {
        var request = new UpdateProductRequest("Ghost", "", 1m, 1, true);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateProductAsync(999, request));
    }

    [Fact]
    public async Task UpdateProductAsync_RejectsNameCollisionWithAnotherProduct()
    {
        Seed(
            new Product { Name = "Chair" },
            new Product { Name = "Desk" });

        var desk = _context.Products.First(p => p.Name == "Desk");
        var request = new UpdateProductRequest("chair", "", 1m, 1, true); // collides with 'Chair'

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateProductAsync(desk.Id, request));
    }

    // ---------- DeactivateProductAsync ----------

    [Fact]
    public async Task DeactivateProductAsync_MarksProductInactive()
    {
        Seed(new Product { Name = "To deactivate", IsActive = true });

        var id = _context.Products.First().Id;
        await _sut.DeactivateProductAsync(id);

        var stored = await _context.Products.FindAsync(id);
        Assert.NotNull(stored);
        Assert.False(stored!.IsActive);
    }

    [Fact]
    public async Task DeactivateProductAsync_ThrowsNotFound_WhenMissing()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.DeactivateProductAsync(424242));
    }
}