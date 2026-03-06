using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Auth;

public class McpApiKeyAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    UserManager<ApplicationUser> userManager)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "McpApiKey";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var apiKey = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(apiKey))
            return AuthenticateResult.Fail("Empty API key.");

        var hash = ComputeHash(apiKey);
        var hashBytes = Convert.FromHexString(hash);

        // Retrieve all non-null hashes and use constant-time comparison to prevent timing attacks
        var user = await userManager.Users
            .Where(u => u.McpApiKeyHash != null)
            .Select(u => new { u.Id, u.McpApiKeyHash, u.UserName, u.Email })
            .ToListAsync()
            .ContinueWith(t => t.Result.FirstOrDefault(u =>
            {
                if (u.McpApiKeyHash is null) return false;
                try
                {
                    var storedBytes = Convert.FromHexString(u.McpApiKeyHash);
                    return CryptographicOperations.FixedTimeEquals(hashBytes, storedBytes);
                }
                catch
                {
                    return false;
                }
            }));

        if (user == null)
            return AuthenticateResult.Fail("Invalid API key.");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id)
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    public static string ComputeHash(string apiKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string GenerateApiKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
