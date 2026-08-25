using System.Net;
using System.Net.Http.Json;
using ProductManagement.Application.DTOs;
using ProductManagement.IntegrationTests.Infra;

namespace ProductManagement.IntegrationTests;

public class AuthApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_WithValidData_Returns200_WithToken()
    {
        var client = _factory.CreateClient();
        var email = $"new-{Guid.NewGuid():N}@test.local";

        var response = await client.PostAsJsonAsync("api/auth/register", new
        {
            email,
            password = "Passw0rd!",
            firstName = "New",
            lastName = "User"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));
        Assert.Equal("User", body.Role);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns400()
    {
        await _factory.EnsureSeededAsync();
        var client = _factory.CreateClient();

        var first = await client.PostAsJsonAsync("api/auth/register", new
        {
            email = "dup@test.local", password = "Passw0rd!", firstName = "A", lastName = "B"
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("api/auth/register", new
        {
            email = "dup@test.local", password = "Passw0rd!", firstName = "C", lastName = "D"
        });

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Register_WithWeakPassword_Returns400_WithIdentityError()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("api/auth/register", new
        {
            email = $"weak-{Guid.NewGuid():N}@test.local",
            password = "weakpass",   // no digit/symbol/uppercase
            firstName = "Weak",
            lastName = "Pw"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(body!.Success);
        Assert.False(string.IsNullOrWhiteSpace(body.Message));
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200_WithTokenAndRole()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("api/auth/login", new
        {
            email = _factory.UserEmail,
            password = CustomWebApplicationFactory.TestPassword
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.Equal("User", body.Role);

        // Basic JWT sanity check: three base64 segments.
        Assert.Equal(2, body.Token.Count(c => c == '.'));
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401_AndGenericMessage()
    {
        await _factory.EnsureSeededAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("api/auth/login", new
        {
            email = _factory.UserEmail,
            password = "WrongPass1!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(body!.Success);
        // Must not reveal whether the account exists.
        Assert.Equal("Invalid email or password.", body.Message);
    }
}