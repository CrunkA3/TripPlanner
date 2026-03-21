# Copilot Instructions for TripPlanner

## Repository Summary
TripPlanner is a personal travel and trip planning web application built with .NET 10, ASP.NET Core Blazor Server, Entity Framework Core, ASP.NET Core Identity, and Microsoft Fluent UI components. It supports multi-user accounts, wishlists with sharing, trip itinerary planning, place management, GPX track handling, AI-assisted place analysis and chat (Ollama / OpenAI), and an MCP (Model Context Protocol) server endpoint.

## Project Type and Stack
- **Language**: C# (.NET 10 / `net10.0`)
- **Framework**: ASP.NET Core Blazor Server (`Microsoft.NET.Sdk.Web`)
- **Orchestration**: .NET Aspire 13.0 (`TripPlanner.AppHost`)
- **Database**: SQL Server (EF Core 10.0.3 with `UseSqlServer`); SQLite is **not** used despite references in the README
- **Auth**: ASP.NET Core Identity with cookie-based auth; `McpApiKeyAuthHandler` for MCP API key auth
- **UI**: Microsoft FluentUI Blazor Components 4.14.0
- **AI**: Pluggable AI provider — Ollama (local LLM) or OpenAI-compatible API, selected via `AI:Provider` config key
- **MCP**: Model Context Protocol server (`ModelContextProtocol.AspNetCore` 1.0.0) served at `/mcp`
- **Testing**: xunit.v3 with Aspire integration testing (`Aspire.Hosting.Testing`)

## Solution Layout
```
TripPlanner.slnx                      # Solution file (XML format)
TripPlanner.AppHost/                  # .NET Aspire orchestration host (entry point for running all services)
  AppHost.cs                          # Registers apiservice and webfrontend (Ollama registration commented out)
  TripPlanner.AppHost.csproj          # Aspire SDK 13.0; references CommunityToolkit.Aspire.Hosting.Ollama 13.1.1
TripPlanner.ApiService/               # Minimal ASP.NET Core Web API (placeholder/weather sample)
  Program.cs
TripPlanner.ServiceDefaults/          # Shared Aspire service defaults (telemetry, health, resilience)
  Extensions.cs
TripPlanner.Web/                      # Main Blazor Server web app
  Program.cs                          # App startup: DI, EF Core, Identity, HTTP clients, repositories, services, MCP
  TripPlanner.Web.csproj              # SQL Server EF Core, FluentUI, Markdig, ReverseMarkdown, ImageSharp, MCP
  appsettings.json                    # Connection string, AI provider, Ollama/OpenAI config
  appsettings.Development.json
  API/                                # Minimal API endpoints
    PlaceImageApi.cs                  # GET /api/placeImages/{imageId}?width= (resize, ETag, Cache-Control)
  Auth/                               # Custom authentication handlers
    McpApiKeyAuthHandler.cs           # Bearer API-key auth scheme for MCP endpoint
  Data/ApplicationDbContext.cs        # EF Core DbContext (Identity + all domain models)
  McpTools/                           # MCP tool classes (exposed via /mcp endpoint)
    PlaceMcpTools.cs
    TripMcpTools.cs
    WishlistMcpTools.cs
  Migrations/                         # EF Core migration files
  Models/                             # Domain entities:
                                      #   ApplicationUser, Wishlist, Place, PlaceImage, PlaceAnalysisResult,
                                      #   PlaceSuggestion, PlaceCategory, Trip, SharedTrip, GpxTrack,
                                      #   Accommodation, UrlImportJob, UrlImportJobStatus, ShareLevel,
                                      #   ChatConversation, ChatJob, ChatJobStatus
  Repositories/                       # Repository interfaces and EF Core implementations
                                      #   IPlaceRepository / PlaceRepository
                                      #   ITripRepository / EfTripRepository
                                      #   IGpxRepository / GpxRepository
                                      #   IWishlistRepository / WishlistRepository
                                      #   IUrlImportJobRepository / UrlImportJobRepository
                                      #   IChatConversationRepository / ChatConversationRepository
                                      #   IChatJobRepository / ChatJobRepository
  Services/                           # Application services
                                      #   GpxService, RoutingService, UserService
                                      #   WeatherService (Open-Meteo API, memory-cached)
                                      #   TransitService (Deutsche Bahn transport.rest API)
                                      #   BrowserTimeZoneService (scoped, set from JS interop in MainLayout)
                                      #   IGeocodingService / NominatimGeocodingService (OpenStreetMap)
                                      #   UrlSecurityHelper (SSRF protection for user-supplied URLs)
                                      #   IPlaceAnalysisService / PlaceAnalysisServiceBase
                                      #     OllamaPlaceAnalysisService, OpenAI/OpenAIPlaceAnalysisService
                                      #   IChatService / OllamaChatService, OpenAI/OpenAIChatService
                                      #   UrlImportBackgroundService (hosted, queue-based URL import)
                                      #   ChatBackgroundService (hosted, queue-based AI chat)
  Components/                         # Blazor components
    App.razor, Routes.razor, _Imports.razor
    Layout/                           # MainLayout.razor, NavMenu.razor, ReconnectModal.razor
    Pages/                            # Home, Counter, Weather, Auth, Privacy, NotFound, Error
      Chat/                           # ChatPage.razor
      Places/                         # PlacesPage.razor
      Wishlists/                      # WishlistsPage.razor, WishlistDetailPage.razor
      Trips/                          # TripsPage.razor, TripPlanPage.razor, TripPlanPage2.razor,
                                      #   TripPlanPanelAccommodations.razor, TripPlanPanelDays.razor,
                                      #   TripPlanPanelPlaces.razor, TripPlanSectionAccomodations.razor,
      Map/                            # MapPage.razor
                                      #   TripPlanSectionDays.razor, TripPlanSectionPlaces.razor
    Account/                          # Identity scaffolded pages (Login, Register, Manage/*)
    Shared/                           # Reusable components:
                                      #   PlaceDialog.razor, PlaceImageHelper.cs, PlaceNotesDrawer.razor,
                                      #   ConfirmDialog.razor, ConfirmDialogContent.cs,
                                      #   WishlistPlacesGrid.razor, MarkdownSection.razor,
                                      #   UrlImportJobDetailsDrawer.razor,
                                      #   GpxFileUpload.razor, CookieConsent.razor
  wwwroot/
    app.css
    js/                               # browserInfo.js, chatInterop.js, cookieConsent.js,
                                      #   heroParallax.js, mapInterop.js, notesEditor.js, orientation.js
scripts/                              # Node.js helper scripts
  take-screenshots.js                 # Playwright-based screenshot automation (used by screenshots.yml CI)
  package.json
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
- `TripPlanner.Web/appsettings.json` — connection string, `AI:Provider`, `Ollama:*`, `OpenAI:*`
- `TripPlanner.Web/appsettings.Development.json` — dev overrides
- `TripPlanner.Web/TripPlanner.Web.csproj` — main project dependencies
- `TripPlanner.AppHost/TripPlanner.AppHost.csproj` — Aspire SDK `13.0`, Ollama hosting toolkit
- `TripPlanner.Tests/TripPlanner.Tests.csproj` — xunit.v3, Aspire.Hosting.Testing

## Key NuGet Packages (TripPlanner.Web)
- `Microsoft.EntityFrameworkCore.SqlServer` 10.0.3 — SQL Server EF Core provider
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.3 — ASP.NET Core Identity
- `Microsoft.FluentUI.AspNetCore.Components` 4.14.0 + `.Icons` — UI component library
- `ModelContextProtocol.AspNetCore` 1.0.0 — MCP server
- `Markdig` 1.1.1 — Markdown rendering (custom `MarkdownSection` component)
- `ReverseMarkdown` 5.2.0 — HTML-to-Markdown conversion (used in AI analysis)
- `SixLabors.ImageSharp` 3.1.12 — server-side image resizing for the Place Image API

## AI Configuration
The AI provider is selected by `AI:Provider` in `appsettings.json` (default `"OpenAI"`):
- **`"Ollama"`** — uses `OllamaChatService` and `OllamaPlaceAnalysisService` with the `"Ollama"` named HTTP client
  - Config: `Ollama:BaseUrl`, `Ollama:Model`, `Ollama:MaxHistoryMessages`
  - Aspire: `CommunityToolkit.Aspire.Hosting.Ollama` (commented out by default in AppHost)
- **`"OpenAI"`** — uses `OpenAIChatService` and `OpenAIPlaceAnalysisService` with the `"OpenAI"` named HTTP client
  - Config: `OpenAI:BaseUrl` (any OpenAI-compatible endpoint), `OpenAI:ApiKey`, `OpenAI:Model`

Both services implement `IChatService` and `IPlaceAnalysisService` respectively and are registered as scoped DI.

## Named HTTP Clients
Registered in `Program.cs`:
| Name | Purpose |
|---|---|
| `OpenMeteo` | Weather forecasts (open-meteo.com) |
| `UrlFetch` | General web page fetch for AI analysis (follows redirects) |
| `UrlFetchNoRedirect` | User-supplied URL fetch with per-hop SSRF validation (no auto-redirect) |
| `Ollama` | Local Ollama LLM (3 min timeout, custom resilience pipeline) |
| `OpenAI` | OpenAI-compatible cloud LLM (3 min timeout, custom resilience pipeline) |
| `Nominatim` | OpenStreetMap geocoding (nominatim.openstreetmap.org) |
| `DbTransit` | Deutsche Bahn ÖPNV/transit search (v6.db.transport.rest, no API key) |

## Architecture Notes
- **Database**: SQL Server only (despite README mentioning SQLite — the actual `Program.cs` uses `UseSqlServer` and the `.csproj` references `Microsoft.EntityFrameworkCore.SqlServer`)
- **DbContext DbSets**: `Wishlists`, `Places`, `PlaceImages`, `Trips`, `TripDays`, `TripPlaces`, `Accommodations`, `GpxTracks`, `GpxPoints`, `UserWishlists`, `SharedTrips`, `UrlImportJobs`, `ChatConversations`, `ChatMessages`, `ChatJobs`
- **Identity**: `RequireConfirmedAccount = true`; uses `IdentityNoOpEmailSender` (no real email in dev); `McpApiKeyAuthHandler` provides a separate Bearer API-key scheme for the MCP endpoint
- **Repositories**: All scoped DI, registered in `Program.cs`: `IPlaceRepository → PlaceRepository`, `ITripRepository → EfTripRepository`, `IGpxRepository → GpxRepository`, `IWishlistRepository → WishlistRepository`, `IUrlImportJobRepository → UrlImportJobRepository`, `IChatConversationRepository → ChatConversationRepository`, `IChatJobRepository → ChatJobRepository`
- **Background Services**: `UrlImportBackgroundService` (queue-based URL-to-place import with AI analysis) and `ChatBackgroundService` (queue-based AI chat job processing) — both registered as `IHostedService`
- **MCP Server**: Served at `/mcp`, secured with `McpApiKeyAuthHandler` Bearer scheme; tools: `TripMcpTools`, `WishlistMcpTools`, `PlaceMcpTools`
- **Place Image API**: REST endpoint at `GET /api/placeImages/{imageId}?width=` — authenticated, supports proportional resize (400/800/1200 px), ETag + Cache-Control headers, served via `PlaceImageApi.cs`
- **SSRF Protection**: `UrlSecurityHelper.IsPrivateOrLocalUri()` blocks loopback, RFC-1918, link-local, and ULA addresses; the `"UrlFetchNoRedirect"` HTTP client follows redirects manually with per-hop validation (max 5 hops) via `PlaceAnalysisServiceBase.AnalyzeUrlAsync`
- **Blazor rendering**: Interactive Server render mode (`AddInteractiveServerComponents`)
- **Localization**: `UseRequestLocalization` accepts all cultures from `Accept-Language`; `BrowserTimeZoneService` (scoped) stores browser timezone + language tag from MainLayout JS interop
- **No linting config** (no `.editorconfig`, `StyleCop`, or custom Roslyn analyzers beyond built-in nullable warnings)
- **CI/CD**: `.github/workflows/codeql.yml` (CodeQL security scanning on push/PR to master) and `.github/workflows/screenshots.yml` (automated Playwright screenshots on release, committed to `assets/screenshots/`)

## Adding New Models
1. Add model class to `TripPlanner.Web/Models/`
2. Add `DbSet<T>` to `TripPlanner.Web/Data/ApplicationDbContext.cs`
3. Create repository interface in `TripPlanner.Web/Repositories/`
4. Implement repository in `TripPlanner.Web/Repositories/`
5. Register in `Program.cs`
6. Run `dotnet ef migrations add <Name>` from `TripPlanner.Web/`

## Code Style Preferences
- **Keep classes small and focused (SRP)**: Each class, service, repository, and component should have one primary responsibility. If a class grows beyond that, split it into multiple smaller, focused classes.
- **Don't repeat yourself (DRY)**: Do not duplicate logic. Extract shared behavior into helper methods, base classes, or services. Identical or near-identical code blocks must be extracted into shared helpers or base types rather than copied.
- **Prefer Blazor components**: Break large Razor pages/components into smaller, reusable child components. Each component should do one thing.
- **Use partial classes**: When a class or Blazor component file becomes long, use C# `partial class` to split the code-behind into multiple files (e.g. `MyPage.razor` + `MyPage.razor.cs` with additional partials like `MyPage.Logic.cs` or `MyPage.EventHandlers.cs`).

## Trust These Instructions
Trust the information in this file. Only search the codebase if something here appears incomplete or incorrect.
