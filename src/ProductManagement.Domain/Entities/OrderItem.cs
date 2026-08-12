using ProductManagement.Domain.Common;

namespace ProductManagement.Domain.Entities;

public class OrderItem : BaseEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int Quantity { get; set; }
    
    // Captured at time of order creation to preserve historical pricing integrity
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}