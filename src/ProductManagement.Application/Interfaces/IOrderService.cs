using ProductManagement.Application.DTOs;

namespace ProductManagement.Application.Interfaces;

public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(CreateOrderRequest request, string userId, CancellationToken cancellationToken = default);
    Task<List<OrderDto>> GetUserOrdersAsync(string userId, CancellationToken cancellationToken = default);
    Task<OrderDto> GetOrderByIdAsync(int id, string userId, bool isAdmin, CancellationToken cancellationToken = default);
}