using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.DTOs;
using ProductManagement.Application.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Services;

public class ProductService : IProductService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ProductService> _logger;

    public ProductService(IApplicationDbContext context, ILogger<ProductService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<ProductDto>> GetProductsAsync(ProductSearchRequest request, CancellationToken cancellationToken = default)
    {
        var query = _context.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            query = query.Where(p => EF.Functions.Like(p.Name, $"%{request.Name}%"));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
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

        return new PagedResult<ProductDto>(items, totalCount, request.PageNumber, request.PageSize);
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
        await _context.SaveChangesAsync(cancellationToken);

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

        await _context.SaveChangesAsync(cancellationToken);

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
}