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

/// <summary>Outcome of a mutating API call, carrying a human-readable server error when it fails.</summary>
public record OperationResult(bool Success, string? Error = null)
{
    public static readonly OperationResult Ok = new(true);
    public static OperationResult Fail(string error) => new(false, error);
}

/// <summary>Subset of RFC7807 ProblemDetails returned by the API's exception middleware.</summary>
internal sealed record ApiProblemDetails(string? Title, string? Detail, Dictionary<string, string[]>? Errors);

internal static class HttpErrorReader
{
    /// <summary>
    /// Extracts a user-friendly message from an error response, preferring the
    /// ProblemDetails detail (e.g. stock/business rule errors), then field
    /// validation errors, then the title.
    /// </summary>
    public static async Task<string> ReadAsync(HttpResponseMessage response)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>();
            if (!string.IsNullOrWhiteSpace(problem?.Detail))
            {
                return problem.Detail;
            }
            if (problem?.Errors is { Count: > 0 })
            {
                return string.Join(" ", problem.Errors.SelectMany(kv => kv.Value));
            }
            if (!string.IsNullOrWhiteSpace(problem?.Title))
            {
                return problem.Title;
            }
        }
        catch
        {
            // Fall through to generic message below.
        }
        return $"Request failed ({(int)response.StatusCode}).";
    }
}

public interface IProductClientService
{
    Task<PagedResult<ProductDto>?> SearchProducts(string name, int pageNumber, int pageSize);
    Task<ProductDto?> GetById(int id);
    Task<OperationResult> Create(CreateProductRequest request);
    Task<OperationResult> Update(int id, UpdateProductRequest request);
    Task<OperationResult> Deactivate(int id);
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

    public async Task<OperationResult> Create(CreateProductRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/products", request);
        return response.IsSuccessStatusCode
            ? OperationResult.Ok
            : OperationResult.Fail(await HttpErrorReader.ReadAsync(response));
    }

    public async Task<OperationResult> Update(int id, UpdateProductRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/products/{id}", request);
        return response.IsSuccessStatusCode
            ? OperationResult.Ok
            : OperationResult.Fail(await HttpErrorReader.ReadAsync(response));
    }

    public async Task<OperationResult> Deactivate(int id)
    {
        var response = await _http.DeleteAsync($"api/products/{id}");
        return response.IsSuccessStatusCode
            ? OperationResult.Ok
            : OperationResult.Fail(await HttpErrorReader.ReadAsync(response));
    }
}

public interface IOrderClientService
{
    Task<(OrderDto? Order, string? Error)> CreateOrder(CreateOrderRequest request);
    Task<List<OrderDto>?> GetMyOrders();
}

public class OrderClientService : IOrderClientService
{
    private readonly HttpClient _http;

    public OrderClientService(HttpClient http)
    {
        _http = http;
    }

    public async Task<(OrderDto? Order, string? Error)> CreateOrder(CreateOrderRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/orders", request);
        if (!response.IsSuccessStatusCode)
        {
            return (null, await HttpErrorReader.ReadAsync(response));
        }
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        return (order, null);
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