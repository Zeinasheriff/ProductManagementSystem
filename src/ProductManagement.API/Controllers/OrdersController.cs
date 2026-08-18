using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductManagement.Application.DTOs;
using ProductManagement.Application.Interfaces;

namespace ProductManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Unable to identify the current user from the access token." });
        }

        var order = await _orderService.CreateOrderAsync(request, userId, ct);
        return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, order);
    }

    [HttpGet]
    public async Task<ActionResult<List<OrderDto>>> GetMyOrders(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Unable to identify the current user from the access token." });
        }

        var orders = await _orderService.GetUserOrdersAsync(userId, ct);
        return Ok(orders);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> GetOrderById(int id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Unable to identify the current user from the access token." });
        }

        bool isAdmin = User.IsInRole("Admin");
        var order = await _orderService.GetOrderByIdAsync(id, userId, isAdmin, ct);
        return Ok(order);
    }

    /// <summary>
    /// Reads the current user's id from the JWT. Checks both the long-form
    /// ClaimTypes.NameIdentifier and the raw short JWT claim names ("nameid"/"sub"),
    /// since inbound claim-type mapping behavior has changed across .NET/JwtBearer
    /// versions and can otherwise silently leave this claim unmapped.
    /// </summary>
    private bool TryGetUserId(out string userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("nameid")
                    ?? User.FindFirstValue("sub");

        userId = value ?? string.Empty;
        return !string.IsNullOrWhiteSpace(userId);
    }
}