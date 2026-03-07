using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;
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
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

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

// Register HttpClient for fetching web page content for AI analysis
builder.Services.AddHttpClient("UrlFetch", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 TripPlanner/1.0");
});

// Register HttpClient for Ollama (local LLM)
// Prefer the Aspire-injected connection string ("ollama"), fall back to explicit config or localhost default
var ollamaBaseUrl = builder.Configuration.GetConnectionString("ollama")
    ?? builder.Configuration["Ollama:BaseUrl"]
    ?? "http://localhost:11434";
builder.Services.AddHttpClient("Ollama", client =>
{
    client.BaseAddress = new Uri(ollamaBaseUrl);
    client.Timeout = TimeSpan.FromMinutes(3);
});

// Register TripPlanner repositories (EF Core)
builder.Services.AddScoped<IPlaceRepository, PlaceRepository>();
builder.Services.AddScoped<ITripRepository, EfTripRepository>();
builder.Services.AddScoped<IGpxRepository, GpxRepository>();
builder.Services.AddScoped<IWishlistRepository, WishlistRepository>();

// Register services
builder.Services.AddScoped<GpxService>();
builder.Services.AddScoped<RoutingService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<WeatherService>();
builder.Services.AddScoped<IPlaceAnalysisService, OllamaPlaceAnalysisService>();

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
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

// Map the MCP endpoint – secured with the MCP API key Bearer scheme
app.MapMcp("/mcp")
    .RequireAuthorization(policy => policy
        .AddAuthenticationSchemes(McpApiKeyAuthHandler.SchemeName)
        .RequireAuthenticatedUser());

app.MapDefaultEndpoints();

app.Run();
