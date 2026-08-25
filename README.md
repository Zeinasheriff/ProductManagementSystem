# Product Management System

A full-stack enterprise-grade Product & Order Management System built with **ASP.NET Core Web API**, **Blazor WebAssembly**, **Entity Framework Core**, **SQL Server**, and **ASP.NET Core Identity**.

## ⚡ Quick Start — All Commands

Run these from the repository root (`ProductManagementSystem/`).

### 1. Build everything

```bash
dotnet restore
dotnet build
```

### 2. One-time setup: JWT signing key *(required before starting the API)*

```bash
cd src/ProductManagement.API
dotnet user-secrets init
dotnet user-secrets set "Jwt:Secret" "PASTE-A-RANDOM-KEY-AT-LEAST-32-CHARS-LONG"
cd ../..
```

> PowerShell one-liner to generate a key: `[Convert]::ToBase64String((1..48 | %{Get-Random -Maximum 256}))`
>
> Alternatives: copy `appsettings.Development.json.example` → `appsettings.Development.json` (git-ignored), or set env var `JWT__Secret`. See [Configuration](#configuration-jwt-signing-key).

### 3. Start the backend API *(Terminal 1)*

```bash
dotnet run --project src/ProductManagement.API
```

| What | URL |
|------|-----|
| API | http://localhost:5071 |
| Swagger UI | http://localhost:5071/swagger |

> First run auto-migrates + seeds SQL LocalDB, demo users, and sample products.

### 4. Start the frontend *(Terminal 2 — keep the API running)*

```bash
dotnet run --project src/ProductManagement.Blazor
```

| What | URL |
|------|-----|
| Blazor app | http://localhost:5017 |

Then sign in with a demo account: `admin@system.local` / `Admin123!` or `user@system.local` / `User123!`

### 5. Run the tests *(no database or internet needed)*

```bash
dotnet test                                                  # all 63 tests
dotnet test tests/ProductManagement.UnitTests                # 43 unit tests only
dotnet test tests/ProductManagement.IntegrationTests         # 20 integration tests only
dotnet test --collect:"XPlat Code Coverage"                  # with coverage report
```

### Handy extras

```bash
# Watch mode during development
dotnet watch --project src/ProductManagement.API
dotnet watch --project src/ProductManagement.Blazor

# Create / apply EF migrations after changing domain models
dotnet ef migrations add MigrationName --project src/ProductManagement.Infrastructure --startup-project src/ProductManagement.API
dotnet ef database update --project src/ProductManagement.Infrastructure --startup-project src/ProductManagement.API

# Publish for deployment
dotnet publish src/ProductManagement.API -c Release -o publish/api
dotnet publish src/ProductManagement.Blazor -c Release -o publish/web
```

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
- Internet access for CDN assets (Bootstrap Icons / Inter font) when using the frontend — the app degrades gracefully offline

## Configuration (JWT signing key)

The JWT secret is **never stored in source control**. `Program.cs` validates it at startup and refuses to run if it is missing or shorter than 32 characters. Pick one of:

**Option A – user secrets (recommended):**

```bash
cd src/ProductManagement.API
dotnet user-secrets init
dotnet user-secrets set "Jwt:Secret" "<paste-a-random-32+-char-key>"
```

Generate a good key quickly, e.g. PowerShell: `[Convert]::ToBase64String((1..48 | %{Get-Random -Maximum 256}))`

**Option B – local development file:**

```bash
cp src/ProductManagement.API/appsettings.Development.json.example src/ProductManagement.API/appsettings.Development.json
# then edit appsettings.Development.json and replace the placeholder key
```

(`appsettings.Development.json` is already git-ignored.)

**Option C – environment variable (CI/production):**

```bash
JWT__Secret="<your-key>"
```

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

The solution contains two test projects (63 tests total). **No database or network is required** — both use the EF Core InMemory provider.

```bash
# Run everything
dotnet test

# Unit tests only (services, validators, DTOs — 43 tests)
dotnet test tests/ProductManagement.UnitTests

# Integration tests only (full HTTP API pipeline — 20 tests)
dotnet test tests/ProductManagement.IntegrationTests

# With code coverage (coverlet.collector is already referenced)
dotnet test --collect:"XPlat Code Coverage"
```

| Project | What it covers |
|---------|----------------|
| `ProductManagement.UnitTests` | `ProductService` (paging clamps, LIKE-wildcard escaping, duplicate names, CRUD), `OrderService` (stock math, totals, consolidation, authorization rules), FluentValidation validators |
| `ProductManagement.IntegrationTests` | Real HTTP requests via `WebApplicationFactory<Program>` + in-memory database: register/login flows, JWT issuance, role-based access (401/403/201 paths), order creation with server-side totals & stock reduction, cross-user order privacy (404) |

Integration tests run the app under the `Testing` environment: SQL Server is swapped for the InMemory provider and the Development-only Swagger UI/seeding are skipped.

## Security Notes

- **Secrets**: JWT signing key lives in user-secrets / env vars / git-ignored dev file — never in the repo. Startup fails fast with guidance if it's absent or weak.
- **Brute force**: auth endpoints are rate limited (10 req/min → HTTP 429 with `Retry-After`), and account lockout (5 failures / 15 min) is enforced via `SignInManager.CheckPasswordSignInAsync(lockoutOnFailure: true)`; failed logins always return a generic message.
- **Transport**: `RequireHttpsMetadata` is relaxed only for local development; security headers (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`) are set on every response.
- **Authorization**: product writes are Admin-only; orders are owner-scoped and foreign order ids answer 404 (no resource probing).
- **Input safety**: all queries are parameterized; product-name search escapes SQL LIKE wildcards; page size is capped at 100; FluentValidation guards every write endpoint.
- **Error handling**: centralized ProblemDetails middleware never leaks stack traces outside Development.
- Known trade-offs (documented, acceptable for this scope): the Blazor client stores its JWT in localStorage (XSS surface typical of WASM SPA+API designs), and registration confirms whether an email is already taken.

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