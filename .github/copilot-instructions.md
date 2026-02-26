# Copilot Instructions for TripPlanner

## Repository Summary
TripPlanner is a personal travel and trip planning web application built with .NET 10, ASP.NET Core Blazor Server, Entity Framework Core, ASP.NET Core Identity, and Microsoft Fluent UI components. It supports multi-user accounts, wishlists with sharing, trip itinerary planning, place management, and GPX track handling.

## Project Type and Stack
- **Language**: C# (.NET 10 / `net10.0`)
- **Framework**: ASP.NET Core Blazor Server (`Microsoft.NET.Sdk.Web`)
- **Orchestration**: .NET Aspire 13.0 (`TripPlanner.AppHost`)
- **Database**: SQL Server (EF Core 10.0.3 with `UseSqlServer`); SQLite is **not** used despite references in the README
- **Auth**: ASP.NET Core Identity with cookie-based auth
- **UI**: Microsoft FluentUI Blazor Components 4.14.0
- **Testing**: xunit.v3 with Aspire integration testing (`Aspire.Hosting.Testing`)

## Solution Layout
```
TripPlanner.slnx                      # Solution file (XML format)
TripPlanner.AppHost/                  # .NET Aspire orchestration host (entry point for running all services)
  AppHost.cs                          # Registers apiservice and webfrontend
TripPlanner.ApiService/               # Minimal ASP.NET Core Web API (placeholder/weather sample)
  Program.cs
TripPlanner.ServiceDefaults/          # Shared Aspire service defaults (telemetry, health, resilience)
  Extensions.cs
TripPlanner.Web/                      # Main Blazor Server web app
  Program.cs                          # App startup: DI registration, EF Core, Identity, repositories, services
  TripPlanner.Web.csproj              # References ServiceDefaults; uses SQL Server EF Core + FluentUI
  appsettings.json                    # Connection string (SQL Server localdb by default)
  appsettings.Development.json
  Data/ApplicationDbContext.cs        # EF Core DbContext (Identity + domain models)
  Migrations/                         # EF Core migration files
  Models/                             # Domain entities: ApplicationUser, Wishlist, Place, Trip, SharedTrip, GpxTrack, Accommodation, PlaceCategory
  Repositories/                       # Repository interfaces and EF Core implementations
  Services/                           # GpxService, RoutingService, UserService
  Components/                         # Blazor components
    App.razor, Routes.razor, _Imports.razor
    Layout/                           # MainLayout.razor, NavMenu.razor
    Pages/                            # Home, Counter, Weather, Auth, Privacy, NotFound, Error
      Wishlists/                      # WishlistsPage.razor, WishlistDetailPage.razor, WishlistAddPlaceDialog.razor
      Wishlist/                       # WishlistPage.razor (legacy)
      Trips/                          # TripsPage.razor, TripPlanPage.razor, TripAddPlaceDialog.razor
      Map/                            # MapPage.razor (placeholder)
    Account/                          # Identity scaffolded pages (Login, Register, Manage/*)
    Shared/                           # GpxFileUpload.razor, WishlistItems.razor, CookieConsent.razor
  wwwroot/
    app.css
    js/                               # cookieConsent.js, heroParallax.js, mapInterop.js, orientation.js
TripPlanner.Tests/                    # Integration tests (Aspire-based, xunit.v3)
  WebTests.cs                         # Single test: GetWebResourceRootReturnsOkStatusCode
Dockerfile                            # Multi-stage build targeting TripPlanner.Web
docker-compose.yml                    # SQL Server + Web containers
```

## Build Instructions

**Runtime required**: .NET SDK 10.0 (e.g. `10.0.102`). Always use .NET 10 SDK.

### Build (from repo root)
```bash
dotnet build
```
Build succeeds with some `MSB4240`/`MSB4241` warnings about Aspire SDK version resolution — these are harmless and expected.

### Run the web app (standalone, no Aspire orchestration)
The app requires SQL Server. The default connection string in `appsettings.json` targets SQL Server localdb:
```
Server=(localdb)\mssqllocaldb;Database=TripPlannerDb;Trusted_Connection=True;MultipleActiveResultSets=true
```
In development, the app auto-applies EF Core migrations on startup (`dbContext.Database.MigrateAsync()`).

```bash
cd TripPlanner.Web
dotnet run
```

### Run via Aspire (orchestrated)
```bash
cd TripPlanner.AppHost
dotnet run
```
This starts both `apiservice` and `webfrontend` with health checks.

### EF Core Migrations
Always run from `TripPlanner.Web/`:
```bash
cd TripPlanner.Web
dotnet ef migrations add <MigrationName>
dotnet ef database update
```
The `dotnet ef` tool must be installed: `dotnet tool install --global dotnet-ef`

### Tests
The integration tests use `Aspire.Hosting.Testing` and start the full Aspire app, requiring a healthy `webfrontend` resource. They have a 30-second timeout and require real infrastructure (SQL Server or a valid connection). Run from repo root:
```bash
dotnet test
```
**Note**: Tests may fail in sandboxed/CI environments without a running SQL Server.

### Docker
Build and run with Docker Compose (starts SQL Server + web app):
```bash
docker-compose up --build
```
Web is exposed on ports `8980` (HTTP) and `8981` (HTTPS).

## Key Configuration Files
- `TripPlanner.Web/appsettings.json` — connection string (SQL Server)
- `TripPlanner.Web/appsettings.Development.json` — dev overrides
- `TripPlanner.Web/TripPlanner.Web.csproj` — main project dependencies (EF Core SqlServer, FluentUI, Identity)
- `TripPlanner.AppHost/TripPlanner.AppHost.csproj` — Aspire SDK `13.0`
- `TripPlanner.Tests/TripPlanner.Tests.csproj` — xunit.v3, Aspire.Hosting.Testing

## Architecture Notes
- **Database**: SQL Server only (despite README mentioning SQLite — the actual `Program.cs` uses `UseSqlServer` and the `.csproj` references `Microsoft.EntityFrameworkCore.SqlServer`)
- **Identity**: `RequireConfirmedAccount = true`; uses `IdentityNoOpEmailSender` (no real email in dev)
- **Repositories**: All scoped DI, registered in `Program.cs`: `IPlaceRepository → PlaceRepository`, `ITripRepository → EfTripRepository`, `IGpxRepository → EfGpxRepository`, `IWishlistRepository → WishlistRepository`
- **Blazor rendering**: Interactive Server render mode (`AddInteractiveServerComponents`)
- **No linting config** (no `.editorconfig`, `StyleCop`, or custom Roslyn analyzers beyond built-in nullable warnings)
- **No CI/CD workflows** in `.github/workflows/` — there are no automated pipelines defined

## Adding New Models
1. Add model class to `TripPlanner.Web/Models/`
2. Add `DbSet<T>` to `TripPlanner.Web/Data/ApplicationDbContext.cs`
3. Create repository interface in `TripPlanner.Web/Repositories/`
4. Implement repository in `TripPlanner.Web/Repositories/`
5. Register in `Program.cs`
6. Run `dotnet ef migrations add <Name>` from `TripPlanner.Web/`

## Trust These Instructions
Trust the information in this file. Only search the codebase if something here appears incomplete or incorrect.
