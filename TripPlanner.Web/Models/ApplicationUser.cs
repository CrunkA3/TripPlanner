using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace TripPlanner.Web.Models;

// Add profile data for application users by adding properties to the ApplicationUser class
public class ApplicationUser : IdentityUser
{
    [MaxLength(100)]
    public string? DisplayName { get; set; }
    public double? HomeLatitude { get; set; }
    public double? HomeLongitude { get; set; }
    public string? HomeLocationName { get; set; }

    [MaxLength(64)]
    /// <summary>
    /// SHA-256 hash (64 lowercase hex characters) of the user's MCP API key.
    /// Null when no key has been generated. Used by <see cref="Auth.McpApiKeyAuthHandler"/>.
    /// </summary>
    public string? McpApiKeyHash { get; set; }

    // Navigation properties
    public List<Wishlist> Wishlists { get; set; } = new();
    public List<UserWishlist> SharedWishlists { get; set; } = new();
    public List<Trip> OwnedTrips { get; set; } = new();
    public List<SharedTrip> SharedTrips { get; set; } = new();
}
