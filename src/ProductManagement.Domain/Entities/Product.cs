using ProductManagement.Domain.Common;

namespace ProductManagement.Domain.Entities;

public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;

    // RowVersion used by EF Core for optimistic concurrency protection on stock updates
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation property
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}