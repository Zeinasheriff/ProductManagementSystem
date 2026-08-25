using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ProductManagement.Application.DTOs;

namespace ProductManagement.IntegrationTests.Infra;

public static class TestClientExtensions
{
    private static readonly ConcurrentDictionary<(string FactoryId, string Email), Task<string>> TokenCache = new();

    /// <summary>
    /// Logs the given seeded user in once per factory/email pair and caches
    /// the resulting JWT (also keeps us comfortably under the auth rate limit).
    /// </summary>
    public static Task<string> GetTokenAsync(this CustomWebApplicationFactory factory, string email) =>
        TokenCache.GetOrAdd((factory.FactoryId, email), _ => LoginAsync(factory, email));

    public static async Task<HttpClient> CreateAuthorizedClientAsync(
        this CustomWebApplicationFactory factory, string email)
    {
        var token = await factory.GetTokenAsync(email);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static Task<HttpClient> CreateAdminClientAsync(this CustomWebApplicationFactory factory) =>
        factory.CreateAuthorizedClientAsync(factory.AdminEmail);

    public static Task<HttpClient> CreateUserClientAsync(this CustomWebApplicationFactory factory) =>
        factory.CreateAuthorizedClientAsync(factory.UserEmail);

    private static async Task<string> LoginAsync(CustomWebApplicationFactory factory, string email)
    {
        await factory.EnsureSeededAsync();

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("api/auth/login",
            new LoginRequest(email, CustomWebApplicationFactory.TestPassword));
        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return auth is { Success: true, Token: { Length: > 0 } token }
            ? token
            : throw new InvalidOperationException($"Login failed during test setup for {email}.");
    }
}