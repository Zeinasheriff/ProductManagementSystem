namespace ProductManagement.Application.DTOs;

public record CreateOrderItemRequest(
    int ProductId,
    int Quantity
);

public record CreateOrderRequest(
    List<CreateOrderItemRequest> Items
);

public record OrderItemDto(
    int Id,
    int ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice
);

public record OrderDto(
    int Id,
    string CreatedByUserId,
    string UserEmail,
    string Status,
    decimal TotalAmount,
    DateTime OrderDate,
    List<OrderItemDto> Items
);