using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;
using TripPlanner.Web.API;
using TripPlanner.Web.Auth;
using TripPlanner.Web.Components;
using TripPlanner.Web.Components.Account;
using TripPlanner.Web.Data;
using TripPlanner.Web.McpTools;
using TripPlanner.Web.Models;
using TripPlanner.Web.Repositories;
using TripPlanner.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddFluentUIComponents();
builder.Services.AddLocalization();
builder.Services.AddMemoryCache(o => o.SizeLimit = 500);

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

var authBuilder = builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    });
authBuilder.AddIdentityCookies();
authBuilder.AddScheme<AuthenticationSchemeOptions, McpApiKeyAuthHandler>(McpApiKeyAuthHandler.SchemeName, _ => { });

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddPooledDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
// Provide a scoped ApplicationDbContext for ASP.NET Core Identity and any code that resolves
// it directly from DI (e.g. background service startup helpers).  Each scope gets a fresh
// instance from the pool which is returned to the pool when the scope is disposed.
builder.Services.AddScoped<ApplicationDbContext>(p =>
    p.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/root/.aspnet/DataProtection-Keys"))
    .SetApplicationName("TripPlanner");

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();


// Register HttpClient for image URL downloads
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("OpenMeteo", client =>
{
    client.BaseAddress = new Uri("https://api.open-meteo.com/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Register HttpClient for Nominatim geocoding (OpenStreetMap)
builder.Services.AddHttpClient("Nominatim", client =>
{
    client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
    client.Timeout = TimeSpan.FromSeconds(10);
    // Nominatim requires a valid User-Agent identifying the application
    client.DefaultRequestHeaders.UserAgent.ParseAdd("TripPlanner/1.0 (https://github.com/CrunkA3/TripPlanner)");
});

// Register HttpClient for Deutsche Bahn transport.rest API (free ÖPNV/transit search, no API key required)
builder.Services.AddHttpClient("DbTransit", client =>
{
    client.BaseAddress = new Uri("https://v6.db.transport.rest/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("TripPlanner/1.0 (https://github.com/CrunkA3/TripPlanner)");
});

// Register TripPlanner repositories (EF Core)
builder.Services.AddScoped<IPlaceRepository, PlaceRepository>();
builder.Services.AddScoped<ITripRepository, TripRepository>();
builder.Services.AddScoped<IGpxRepository, GpxRepository>();
builder.Services.AddScoped<IWishlistRepository, WishlistRepository>();
builder.Services.AddScoped<IUrlImportJobRepository, UrlImportJobRepository>();
builder.Services.AddScoped<IChatConversationRepository, ChatConversationRepository>();
builder.Services.AddScoped<IChatJobRepository, ChatJobRepository>();
builder.Services.AddScoped<IPlaceCollectionRepository, PlaceCollectionRepository>();

// Register services
builder.Services.AddScoped<GpxService>();
builder.Services.AddScoped<RoutingService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<WeatherService>();
builder.Services.AddScoped<TransitService>();
builder.Services.AddScoped<BrowserTimeZoneService>();
builder.Services.AddScoped<IGeocodingService, NominatimGeocodingService>();
builder.Services.AddScoped<WishlistImportExportService>();
builder.Services.AddScoped<TripMapExportService>();

builder.AddLlmServices();

builder.Services.AddHostedService<UrlImportBackgroundService>();
builder.Services.AddHostedService<ChatBackgroundService>();

// Register HttpContextAccessor for MCP tools
builder.Services.AddHttpContextAccessor();

// Register MCP server
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<TripMcpTools>()
    .WithTools<WishlistMcpTools>()
    .WithTools<PlaceMcpTools>();


var app = builder.Build();


// Configure the HTTP request pipeline.
var applyMigrationsOnStartup = app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true);
var startupMigrationTimeoutSeconds = app.Configuration.GetValue("Database:StartupMigrationTimeoutSeconds", 120);

if (applyMigrationsOnStartup)
{
    app.Logger.LogInformation(
        "Applying database migrations on startup with timeout {TimeoutSeconds}s.",
        startupMigrationTimeoutSeconds);

    using var migrationTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(startupMigrationTimeoutSeconds));
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    try
    {
        await dbContext.Database.MigrateAsync(migrationTimeout.Token);
    }
    catch (OperationCanceledException ex) when (migrationTimeout.IsCancellationRequested)
    {
        app.Logger.LogCritical(
            ex,
            "Database migration timed out after {TimeoutSeconds}s during startup. " +
            "Increase 'Database:StartupMigrationTimeoutSeconds' or set " +
            "'Database:ApplyMigrationsOnStartup' to false to skip migrations on startup.",
            startupMigrationTimeoutSeconds);
        throw;
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex, "Failed to apply database migrations during startup.");
        throw;
    }
}
else
{
    app.Logger.LogInformation("Skipping database migrations on startup (Database:ApplyMigrationsOnStartup=false).");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRequestLocalization(options =>
{
    // Accept any culture the browser declares via Accept-Language.
    // Without an explicit SupportedCultures list the middleware skips culture negotiation
    // and always falls back to the default, so we enumerate all available cultures.
    var allCultureNames = System.Globalization.CultureInfo
        .GetCultures(System.Globalization.CultureTypes.AllCultures)
        .Where(c => !string.IsNullOrEmpty(c.Name))
        .Select(c => c.Name)
        .ToArray();
    options.AddSupportedCultures(allCultureNames);
    options.AddSupportedUICultures(allCultureNames);
    options.SetDefaultCulture("en-US");
});
app.UseAntiforgery();

// Map the MCP endpoint – secured with the MCP API key Bearer scheme
// Antiforgery is disabled here because MCP uses Bearer token authentication, not cookies,
// and to avoid antiforgery and Blazor router conflicts for this non-browser API endpoint
app.MapMcp("/mcp")
    .RequireAuthorization(policy => policy
        .AddAuthenticationSchemes(McpApiKeyAuthHandler.SchemeName)
        .RequireAuthenticatedUser())
    .DisableAntiforgery();

// Map API endpoints
app.MapPlaceImageApi();
app.MapGpxDownloadApi();
app.MapTripImageExportApi();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.MapDefaultEndpoints();

app.Run();
