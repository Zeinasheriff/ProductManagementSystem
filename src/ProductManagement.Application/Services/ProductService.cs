using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.DTOs;
using ProductManagement.Application.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Services;

public class ProductService : IProductService
{
    // Upper bound for any page request, preventing unbounded result sets.
    public const int MaxPageSize = 100;

    private readonly IApplicationDbContext _context;
    private readonly ILogger<ProductService> _logger;

    public ProductService(IApplicationDbContext context, ILogger<ProductService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<ProductDto>> GetProductsAsync(ProductSearchRequest request, CancellationToken cancellationToken = default)
    {
        // Clamp caller-supplied paging values so they can never produce a
        // negative Skip, a division-by-zero, or unbounded result sets.
        int pageNumber = Math.Max(1, request.PageNumber);
        int pageSize = Math.Clamp(request.PageSize <= 0 ? 10 : request.PageSize, 1, MaxPageSize);

        var query = _context.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            string pattern = $"%{EscapeLikePattern(request.Name.Trim())}%";
            query = query.Where(p => EF.Functions.Like(p.Name, pattern, "\\"));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.Description,
                p.Price,
                p.StockQuantity,
                p.IsActive,
                p.CreatedAt,
                p.UpdatedAt
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<ProductDto> GetProductByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var p = await _context.Products.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (p == null) throw new NotFoundException($"Product with ID {id} was not found.");

        return new ProductDto(p.Id, p.Name, p.Description, p.Price, p.StockQuantity, p.IsActive, p.CreatedAt, p.UpdatedAt);
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        bool exists = await _context.Products.AnyAsync(p => p.Name.ToLower() == request.Name.ToLower(), cancellationToken);
        if (exists)
        {
            throw new BadRequestException($"A product with the name '{request.Name}' already exists.");
        }

        var product = new Product
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Two concurrent creates can pass the existence check above; the
            // unique index on Name then rejects the loser at the DB level.
            _logger.LogWarning(ex, "Duplicate product name insert raced with another request: {ProductName}", request.Name);
            throw new BadRequestException($"A product with the name '{request.Name}' already exists.");
        }

        _logger.LogInformation("Product {ProductId} created: {ProductName}", product.Id, product.Name);

        return new ProductDto(product.Id, product.Name, product.Description, product.Price, product.StockQuantity, product.IsActive, product.CreatedAt, product.UpdatedAt);
    }

    public async Task<ProductDto> UpdateProductAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product == null) throw new NotFoundException($"Product with ID {id} was not found.");

        bool duplicateName = await _context.Products
            .AnyAsync(p => p.Name.ToLower() == request.Name.ToLower() && p.Id != id, cancellationToken);
        if (duplicateName)
        {
            throw new BadRequestException($"Another product with the name '{request.Name}' already exists.");
        }

        product.Name = request.Name;
        product.Description = request.Description;
        product.Price = request.Price;
        product.StockQuantity = request.StockQuantity;
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Duplicate product name raced during update: {ProductName}", request.Name);
            throw new BadRequestException($"Another product with the name '{request.Name}' already exists.");
        }

        _logger.LogInformation("Product {ProductId} updated.", product.Id);

        return new ProductDto(product.Id, product.Name, product.Description, product.Price, product.StockQuantity, product.IsActive, product.CreatedAt, product.UpdatedAt);
    }

    public async Task DeactivateProductAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product == null) throw new NotFoundException($"Product with ID {id} was not found.");

        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Product {ProductId} deactivated.", id);
    }

    /// <summary>
    /// Escapes SQL LIKE wildcards so user input cannot inject pattern
    /// characters ('%', '_', '[') into name searches. Paired with the
    /// explicit escape character '\' passed to EF.Functions.Like.
    /// </summary>
    private static string EscapeLikePattern(string input) =>
        input
            .Replace(@"\", @"\\")
            .Replace("%", @"\%")
            .Replace("_", "\\_")
            .Replace("[", "\\[");
}