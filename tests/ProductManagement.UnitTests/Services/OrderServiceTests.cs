using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.DTOs;
using ProductManagement.Application.Services;
using ProductManagement.Domain.Entities;
using ProductManagement.Domain.Enums;
using ProductManagement.Infrastructure.Persistence;
using ProductManagement.UnitTests.Helpers;

namespace ProductManagement.UnitTests.Services;

public class OrderServiceTests : IDisposable
{
    private const string UserId = "user-1";

    private readonly ApplicationDbContext _context;
    private readonly OrderService _sut;

    public OrderServiceTests()
    {
        _context = TestDbContextFactory.Create();

        // Mirror production: orders always reference an existing user row.
        _context.Users.AddRange(
            new ApplicationUser { Id = UserId, UserName = "u1@test.local", Email = "u1@test.local" },
            new ApplicationUser { Id = "user-2", UserName = "u2@test.local", Email = "u2@test.local" },
            new ApplicationUser { Id = "admin-id", UserName = "adm@test.local", Email = "adm@test.local" });
        _context.SaveChanges();

        _sut = new OrderService(_context, NullLogger<OrderService>.Instance);
    }

    public void Dispose() => _context.Dispose();

    private Product AddProduct(string name, decimal price, int stock, bool isActive = true)
    {
        var p = new Product { Name = name, Price = price, StockQuantity = stock, IsActive = isActive };
        _context.Products.Add(p);
        _context.SaveChanges();
        return p;
    }

    // ---------- CreateOrderAsync ----------

    [Fact]
    public async Task CreateOrder_Success_ReducesStock_AndCalculatesTotal()
    {
        var laptop = AddProduct("Laptop", 1000m, 10);
        var mouse = AddProduct("Mouse", 25m, 50);

        var request = new CreateOrderRequest(new List<CreateOrderItemRequest>
        {
            new(laptop.Id, 2),
            new(mouse.Id, 4)
        });

        var order = await _sut.CreateOrderAsync(request, UserId);

        Assert.Equal(2100m, order.TotalAmount); // 2*1000 + 4*25
        Assert.Equal(2, order.Items.Count);

        var laptopAfter = await _context.Products.AsNoTracking().SingleAsync(p => p.Id == laptop.Id);
        var mouseAfter = await _context.Products.AsNoTracking().SingleAsync(p => p.Id == mouse.Id);
        Assert.Equal(8, laptopAfter.StockQuantity);
        Assert.Equal(46, mouseAfter.StockQuantity);
    }

    [Fact]
    public async Task CreateOrder_CapturesUnitPrice_AtOrderTime()
    {
        var product = AddProduct("Widget", 30m, 100);

        var order = await _sut.CreateOrderAsync(
            new CreateOrderRequest(new List<CreateOrderItemRequest> { new(product.Id, 1) }), UserId);

        Assert.Equal(30m, order.Items.Single().UnitPrice);
    }

    [Fact]
    public async Task CreateOrder_ConsolidatesDuplicateProductLines()
    {
        var product = AddProduct("Cable", 5m, 100);

        var order = await _sut.CreateOrderAsync(
            new CreateOrderRequest(new List<CreateOrderItemRequest>
            {
                new(product.Id, 1),
                new(product.Id, 2)
            }), UserId);

        var line = Assert.Single(order.Items);
        Assert.Equal(3, line.Quantity);
        Assert.Equal(15m, line.TotalPrice);

        var after = await _context.Products.AsNoTracking().SingleAsync(p => p.Id == product.Id);
        Assert.Equal(97, after.StockQuantity);
    }

    [Fact]
    public async Task CreateOrder_EmptyItems_ThrowsBadRequest()
    {
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _sut.CreateOrderAsync(new CreateOrderRequest(new List<CreateOrderItemRequest>()), UserId));
    }

    [Fact]
    public async Task CreateOrder_NullItems_ThrowsBadRequest()
    {
        await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateOrderAsync(new CreateOrderRequest(null!), UserId));
    }

    [Fact]
    public async Task CreateOrder_ProductNotFound_ThrowsBadRequest()
    {
        var request = new CreateOrderRequest(new List<CreateOrderItemRequest> { new(9999, 1) });

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateOrderAsync(request, UserId));
    }

    [Fact]
    public async Task CreateOrder_InactiveProduct_ThrowsBadRequest()
    {
        var product = AddProduct("Retired Item", 10m, 5, isActive: false);

        var request = new CreateOrderRequest(new List<CreateOrderItemRequest> { new(product.Id, 1) });

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateOrderAsync(request, UserId));
    }

    [Fact]
    public async Task CreateOrder_InsufficientStock_ThrowsBadRequest_AndDoesNotCreateOrder()
    {
        var product = AddProduct("Scarce Item", 10m, 2);

        var request = new CreateOrderRequest(new List<CreateOrderItemRequest> { new(product.Id, 5) });

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateOrderAsync(request, UserId));

        Assert.Equal(0, await _context.Orders.CountAsync());
    }

    // ---------- GetUserOrdersAsync ----------

    [Fact]
    public async Task GetUserOrders_ReturnsOnlyOrdersForRequestedUser()
    {
        var product = AddProduct("Gadget", 50m, 10);

        await _sut.CreateOrderAsync(
            new CreateOrderRequest(new List<CreateOrderItemRequest> { new(product.Id, 1) }), UserId);
        await _sut.CreateOrderAsync(
            new CreateOrderRequest(new List<CreateOrderItemRequest> { new(product.Id, 1) }), "user-2");

        var myOrders = await _sut.GetUserOrdersAsync(UserId);

        Assert.Single(myOrders);
        Assert.All(myOrders, o => Assert.Equal(UserId, o.CreatedByUserId));
    }

    // ---------- GetOrderByIdAsync ----------

    [Fact]
    public async Task GetOrderById_OwnerCanView()
    {
        var product = AddProduct("Thing", 12m, 5);
        var created = await _sut.CreateOrderAsync(
            new CreateOrderRequest(new List<CreateOrderItemRequest> { new(product.Id, 2) }), UserId);

        var dto = await _sut.GetOrderByIdAsync(created.Id, UserId, isAdmin: false);

        Assert.Equal(created.Id, dto.Id);
        Assert.Equal(24m, dto.TotalAmount);
    }

    [Fact]
    public async Task GetOrderById_NonOwnerWithoutAdmin_ThrowsNotFound()
    {
        var product = AddProduct("Secret", 99m, 5);
        var created = await _sut.CreateOrderAsync(
            new CreateOrderRequest(new List<CreateOrderItemRequest> { new(product.Id, 1) }), UserId);

        // 404 rather than a 403-style error so outsiders cannot probe for
        // the existence of other users' order ids.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.GetOrderByIdAsync(created.Id, "someone-else", isAdmin: false));
    }

    [Fact]
    public async Task GetOrderById_AdminCanViewAnyUsersOrder()
    {
        var product = AddProduct("Shared", 7m, 5);
        var created = await _sut.CreateOrderAsync(
            new CreateOrderRequest(new List<CreateOrderItemRequest> { new(product.Id, 1) }), UserId);

        var dto = await _sut.GetOrderByIdAsync(created.Id, "admin-id", isAdmin: true);

        Assert.Equal(created.Id, dto.Id);
    }

    [Fact]
    public async Task GetOrderById_Missing_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetOrderByIdAsync(31337, UserId, isAdmin: false));
    }
}