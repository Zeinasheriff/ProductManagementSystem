using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Infrastructure.Persistence;

public static class DbInitializer
{
    public static async Task SeedAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        await context.Database.MigrateAsync();

        // Seed Roles
        string[] roles = { "Admin", "User" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // Seed Admin User
        string adminEmail = "admin@system.local";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "System",
                LastName = "Admin",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(adminUser, "Admin123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }

        // Seed Standard User
        string userEmail = "user@system.local";
        if (await userManager.FindByEmailAsync(userEmail) == null)
        {
            var standardUser = new ApplicationUser
            {
                UserName = userEmail,
                Email = userEmail,
                FirstName = "Jane",
                LastName = "Doe",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(standardUser, "User123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(standardUser, "User");
            }
        }

        // Seed Products
        if (!await context.Products.AnyAsync())
        {
            context.Products.AddRange(
                new Product { Name = "Developer Laptop Pro", Description = "High performance laptop with 32GB RAM", Price = 1899.99m, StockQuantity = 25, IsActive = true },
                new Product { Name = "Ergonomic Mechanical Keyboard", Description = "RGB split mechanical keyboard with tactile switches", Price = 149.50m, StockQuantity = 100, IsActive = true },
                new Product { Name = "4K UltraHD Monitor 32-inch", Description = "IPS panel with 144Hz refresh rate and HDR", Price = 599.00m, StockQuantity = 15, IsActive = true },
                new Product { Name = "Noise Cancelling Headphones", Description = "Wireless over-ear headphones with 30-hour battery life", Price = 249.99m, StockQuantity = 50, IsActive = true }
            );
            await context.SaveChangesAsync();
        }
    }
}