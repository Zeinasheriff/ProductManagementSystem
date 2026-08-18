using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.DTOs;
using ProductManagement.Application.Interfaces;
using ProductManagement.Domain.Entities;
using ProductManagement.Domain.Enums;

namespace ProductManagement.Application.Services;

public class OrderService : IOrderService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<OrderService> _logger;

    public OrderService(IApplicationDbContext context, ILogger<OrderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<OrderDto> CreateOrderAsync(CreateOrderRequest request, string userId, CancellationToken cancellationToken = default)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            throw new BadRequestException("Order must contain at least one item.");
        }

        // Group duplicate requested products into single line items
        var consolidatedItems = request.Items
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToList();

        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);

        try
        {
            var productIds = consolidatedItems.Select(i => i.ProductId).ToList();

            // Fetch products with tracking for updates
            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken);

            var order = new Order
            {
                CreatedByUserId = userId,
                OrderDate = DateTime.UtcNow,
                Status = OrderStatus.Completed,
                CreatedAt = DateTime.UtcNow
            };

            decimal calculatedTotal = 0m;

            foreach (var item in consolidatedItems)
            {
                if (!products.TryGetValue(item.ProductId, out var product))
                {
                    throw new BadRequestException($"Product with ID {item.ProductId} does not exist.");
                }

                if (!product.IsActive)
                {
                    throw new BadRequestException($"Product '{product.Name}' (ID: {product.Id}) is inactive and cannot be ordered.");
                }

                if (product.StockQuantity < item.Quantity)
                {
                    throw new BadRequestException($"Insufficient stock for product '{product.Name}'. Requested: {item.Quantity}, Available: {product.StockQuantity}.");
                }

                // Decrement stock
                product.StockQuantity -= item.Quantity;
                product.UpdatedAt = DateTime.UtcNow;

                decimal itemTotal = product.Price * item.Quantity;
                calculatedTotal += itemTotal;

                order.OrderItems.Add(new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price, // Preserve historical price
                    TotalPrice = itemTotal,
                    CreatedAt = DateTime.UtcNow
                });
            }

            order.TotalAmount = calculatedTotal;
            _context.Orders.Add(order);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Order {OrderId} successfully created for user {UserId}. Total: {TotalAmount}", order.Id, userId, order.TotalAmount);

            return await GetOrderByIdAsync(order.Id, userId, true, cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning(ex, "Concurrency conflict detected during stock reduction.");
            throw new ConcurrencyConflictException("The order could not be completed because product stock was updated by another request. Please try again.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to create order for user {UserId}. Transaction rolled back.", userId);
            throw;
        }
    }

    public async Task<List<OrderDto>> GetUserOrdersAsync(string userId, CancellationToken cancellationToken = default)
    {
        var orders = await _context.Orders
            .AsNoTracking()
            .Include(o => o.User)
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
            .Where(o => o.CreatedByUserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        // MapToDto is a C# method that cannot be translated to SQL,
        // so we materialize the entities first, then map in memory.
        return orders.Select(MapToDto).ToList();
    }

    public async Task<OrderDto> GetOrderByIdAsync(int id, string userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.User)
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order == null) throw new NotFoundException($"Order with ID {id} was not found.");

        if (!isAdmin && order.CreatedByUserId != userId)
        {
            throw new BadRequestException("You are not authorized to view this order.");
        }

        return MapToDto(order);
    }

    private static OrderDto MapToDto(Order o) => new(
        o.Id,
        o.CreatedByUserId,
        o.User?.Email ?? "N/A",
        o.Status.ToString(),
        o.TotalAmount,
        o.OrderDate,
        o.OrderItems.Select(i => new OrderItemDto(
            i.Id,
            i.ProductId,
            i.Product?.Name ?? "Unknown Product",
            i.Quantity,
            i.UnitPrice,
            i.TotalPrice
        )).ToList()
    );
}