using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ProductManagement.Domain.Entities;
using ProductManagement.Infrastructure.Persistence;

namespace ProductManagement.IntegrationTests.Infra;

/// <summary>
/// Boots the real API pipeline with an EF Core InMemory database instead of
/// SQL Server. Runs under the "Testing" environment so the Development-only
/// Swagger UI and database seeding/migrations are skipped.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestSigningKey = "TEST_SIGNING_KEY_FOR_INTEGRATION_TESTS_ONLY_32CH!";

    static CustomWebApplicationFactory()
    {
        // The test hosts the API in-process, so setting the environment
        // variable here feeds the app's configuration provider directly.
        // Env vars outrank appsettings*.json, which satisfies Program.cs'
        // signing-key validation deterministically regardless of the
        // configuration-source ordering quirks of the minimal hosting model.
        Environment.SetEnvironmentVariable("JWT__Secret", TestSigningKey);
        Environment.SetEnvironmentVariable("JWT__Issuer", "ProductManagementAPI");
        Environment.SetEnvironmentVariable("JWT__Audience", "ProductManagementClient");
    }

    private readonly string _dbName = $"TestDb_{Guid.NewGuid():N}";
    private readonly SemaphoreSlim _seedLock = new(1, 1);
    private bool _seeded;

    public const string TestPassword = "Passw0rd!";
    public string AdminEmail { get; } = $"admin-{Guid.NewGuid():N}@test.local";
    public string UserEmail { get; } = $"user-{Guid.NewGuid():N}@test.local";
    public string FactoryId { get; } = Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Swap the SQL Server registration for the InMemory provider.
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_dbName)
                       .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        });
    }

    /// <summary>Creates roles and the two test users exactly once per factory.</summary>
    public async Task EnsureSeededAsync()
    {
        if (_seeded)
        {
            return;
        }

        await _seedLock.WaitAsync();
        try
        {
            if (_seeded)
            {
                return;
            }

            using var scope = Services.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in new[] { "Admin", "User" })
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            await CreateUserAsync(userManager, AdminEmail, "Ada", "Admin", isAdmin: true);
            await CreateUserAsync(userManager, UserEmail, "Ursula", "User", isAdmin: false);

            _seeded = true;
        }
        finally
        {
            _seedLock.Release();
        }
    }

    private static async Task CreateUserAsync(
        UserManager<ApplicationUser> userManager, string email, string first, string last, bool isAdmin)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = first,
            LastName = last,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, CustomWebApplicationFactory.TestPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to seed user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        await userManager.AddToRoleAsync(user, isAdmin ? "Admin" : "User");
    }
}