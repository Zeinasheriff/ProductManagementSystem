using ProductManagement.Domain.Common;
using ProductManagement.Domain.Enums;

namespace ProductManagement.Domain.Entities;

public class Order : BaseEntity
{
    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}