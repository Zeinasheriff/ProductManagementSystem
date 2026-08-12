# ProductManagementSystem

A full-stack enterprise-grade Product & Order Management System built with ASP.NET Core Web API, Blazor WebAssembly, Entity Framework Core, SQL Server, and ASP.NET Core Identity.

## Architecture & Principles
- **Clean Architecture & SOLID:** Strict separation into Domain, Application, Infrastructure, API, and Client layers.
- **Optimistic Concurrency Protection:** EF Core `RowVersion` columns ensure parallel order submissions never oversell inventory.
- **Historical Price Integrity:** Order items lock in current `Product.Price` at the time of creation.
- **Role-Based Authorization:** Custom JWT token handler enforcing Admin vs User permissions.

## Database Migrations
Run these commands from the root directory:
```bash
dotnet ef migrations add InitialCreate --project src/ProductManagement.Infrastructure --startup-project src/ProductManagement.API
dotnet ef database update --project src/ProductManagement.Infrastructure --startup-project src/ProductManagement.API