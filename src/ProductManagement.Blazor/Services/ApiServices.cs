using System.Net.Http.Json;
using ProductManagement.Application.DTOs;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;

namespace ProductManagement.Blazor.Services;

public interface IAuthService
{
    Task<AuthResponse> Login(LoginRequest request);
    Task<AuthResponse> Register(RegisterRequest request);
    Task Logout();
}

public class AuthService : IAuthService
{
    private readonly HttpClient _http;
    private readonly ILocalStorageService _localStorage;
    private readonly AuthenticationStateProvider _authStateProvider;

    public AuthService(HttpClient http, ILocalStorageService localStorage, AuthenticationStateProvider authStateProvider)
    {
        _http = http;
        _localStorage = localStorage;
        _authStateProvider = authStateProvider;
    }

    public async Task<AuthResponse> Login(LoginRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", request);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();

        if (response.IsSuccessStatusCode && result != null && result.Success)
        {
            await _localStorage.SetItemAsync("authToken", result.Token);
            if (_authStateProvider is CustomAuthStateProvider customProvider)
            {
                customProvider.NotifyUserAuthentication(result.Token);
            }
        }

        return result ?? new AuthResponse(false, "", "", "", "Failed to process authentication.");
    }

    public async Task<AuthResponse> Register(RegisterRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/auth/register", request);
        return await response.Content.ReadFromJsonAsync<AuthResponse>()
               ?? new AuthResponse(false, "", "", "", "Registration failed.");
    }

    public async Task Logout()
    {
        // Always clear the token first so the JWT auth handler stops sending it.
        try
        {
            await _localStorage.RemoveItemAsync("authToken");
        }
        finally
        {
            if (_authStateProvider is CustomAuthStateProvider customProvider)
            {
                customProvider.NotifyUserLogout();
            }
        }
    }
}

public interface IProductClientService
{
    Task<PagedResult<ProductDto>?> SearchProducts(string name, int pageNumber, int pageSize);
    Task<ProductDto?> GetById(int id);
    Task<bool> Create(CreateProductRequest request);
    Task<bool> Update(int id, UpdateProductRequest request);
    Task<bool> Deactivate(int id);
}

public class ProductClientService : IProductClientService
{
    private readonly HttpClient _http;

    public ProductClientService(HttpClient http)
    {
        _http = http;
    }

    public async Task<PagedResult<ProductDto>?> SearchProducts(string name, int pageNumber, int pageSize)
    {
        return await _http.GetFromJsonAsync<PagedResult<ProductDto>>($"api/products/search?name={Uri.EscapeDataString(name)}&pageNumber={pageNumber}&pageSize={pageSize}");
    }

    public async Task<ProductDto?> GetById(int id)
    {
        return await _http.GetFromJsonAsync<ProductDto>($"api/products/{id}");
    }

    public async Task<bool> Create(CreateProductRequest request)
    {
        var res = await _http.PostAsJsonAsync("api/products", request);
        return res.IsSuccessStatusCode;
    }

    public async Task<bool> Update(int id, UpdateProductRequest request)
    {
        var res = await _http.PutAsJsonAsync($"api/products/{id}", request);
        return res.IsSuccessStatusCode;
    }

    public async Task<bool> Deactivate(int id)
    {
        var res = await _http.DeleteAsync($"api/products/{id}");
        return res.IsSuccessStatusCode;
    }
}

public interface IOrderClientService
{
    Task<OrderDto?> CreateOrder(CreateOrderRequest request);
    Task<List<OrderDto>?> GetMyOrders();
}

public class OrderClientService : IOrderClientService
{
    private readonly HttpClient _http;

    public OrderClientService(HttpClient http)
    {
        _http = http;
    }

    public async Task<OrderDto?> CreateOrder(CreateOrderRequest request)
    {
        var res = await _http.PostAsJsonAsync("api/orders", request);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<OrderDto>();
    }

    public async Task<List<OrderDto>?> GetMyOrders()
    {
        try
        {
            return await _http.GetFromJsonAsync<List<OrderDto>>("api/orders");
        }
        catch (Exception)
        {
            return new List<OrderDto>();
        }
    }
}