# Product Management System

A full-stack enterprise-grade Product & Order Management System built with **ASP.NET Core Web API**, **Blazor WebAssembly**, **Entity Framework Core**, **SQL Server**, and **ASP.NET Core Identity**.

## Features

- **Products**: View, add, edit, and deactivate products
- **Search**: Search products by name (with pagination)
- **Orders**: Create orders with one or more products
- **Stock Validation**: Validates stock before creating an order
- **Automatic Total Calculation**: Order total is calculated server-side
- **Stock Reduction**: Reduces product stock after a successful order
- **Authentication**: JWT-based auth with Admin/User roles (ASP.NET Core Identity)
- **Input Validation**: FluentValidation for all API requests
- **Error Handling**: Centralized exception middleware returning ProblemDetails
- **Swagger**: Full OpenAPI documentation with JWT Bearer support
- **Optimistic Concurrency**: RowVersion prevents overselling inventory
- **Rate Limiting**: Protects auth endpoints from brute-force attacks

## Architecture

- **Clean Architecture & SOLID** principles
- **Layers**:
  - `ProductManagement.Domain` — Entities, enums, base classes
  - `ProductManagement.Application` — DTOs, validators, services, interfaces
  - `ProductManagement.Infrastructure` — EF Core DbContext, Identity, migrations
  - `ProductManagement.API` — Controllers, middleware, Swagger
  - `ProductManagement.Blazor` — Blazor WebAssembly frontend

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server LocalDB](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb) (comes with Visual Studio) or SQL Server Express

## Getting Started

### 1. Restore and Build

```bash
dotnet restore
dotnet build
```

### 2. Run the API (Terminal 1)

```bash
dotnet run --project src/ProductManagement.API
```

- API runs at: **http://localhost:5071**
- Swagger UI: **http://localhost:5071/swagger**

> The API **auto-migrates and seeds the database** on startup in Development mode. If you prefer to run migrations manually:

```bash
dotnet ef database update --project src/ProductManagement.Infrastructure --startup-project src/ProductManagement.API
```

### 3. Run the Blazor Frontend (Terminal 2)

```bash
dotnet run --project src/ProductManagement.Blazor
```

- Blazor runs at: **http://localhost:5017**

> **Important:** Both the API and the Blazor app must be running simultaneously. The Blazor app calls the API on port 5071.

## Demo Accounts

The database seeder creates the following accounts:

| Role  | Email              | Password   |
|-------|--------------------|------------|
| Admin | admin@system.local | `Admin123!` |
| User  | user@system.local  | `User123!`  |

- **Admin** can create, edit, and deactivate products.
- **User** (and Admin) can view products and create orders.
- Use the **Register** page to create a new user account.

## API Endpoints

### Auth
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/register` | Register a new user |
| POST | `/api/auth/login` | Login and receive a JWT |

### Products
| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| GET | `/api/products/search?name=&pageNumber=&pageSize=` | Search/paginate products | Public |
| GET | `/api/products/{id}` | Get product by ID | Public |
| POST | `/api/products` | Create product | Admin |
| PUT | `/api/products/{id}` | Update product | Admin |
| DELETE | `/api/products/{id}` | Deactivate product | Admin |

### Orders
| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/api/orders` | Create an order (validates stock, calculates total, reduces stock) | Authenticated |
| GET | `/api/orders` | Get current user's orders | Authenticated |
| GET | `/api/orders/{id}` | Get order by ID | Authenticated (owner/admin) |

## Seed Data

The database is seeded with:
- **Roles**: `Admin`, `User`
- **Users**: Admin and a standard user (see Demo Accounts above)
- **Products**: 4 sample products (laptop, keyboard, monitor, headphones)

## Database Migrations

If you make model changes, create and apply a new migration:

```bash
dotnet ef migrations add MigrationName --project src/ProductManagement.Infrastructure --startup-project src/ProductManagement.API
dotnet ef database update --project src/ProductManagement.Infrastructure --startup-project src/ProductManagement.API
```

## Running Tests

```bash
dotnet test
```

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | ASP.NET Core 8 Web API |
| Frontend | Blazor WebAssembly (.NET 8) |
| ORM | Entity Framework Core 8 |
| Database | SQL Server (LocalDB) |
| Auth | ASP.NET Core Identity + JWT Bearer |
| Validation | FluentValidation |
| API Docs | Swashbuckle (Swagger) |
| Testing | xUnit |