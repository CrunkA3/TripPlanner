using Microsoft.AspNetCore.Identity;

namespace TripPlanner.Web.Models;

// Add profile data for application users by adding properties to the ApplicationUser class
public class ApplicationUser : IdentityUser
{
    public double? HomeLatitude { get; set; }
    public double? HomeLongitude { get; set; }
    public string? HomeLocationName { get; set; }

    // Navigation properties
    public List<Wishlist> Wishlists { get; set; } = new();
    public List<UserWishlist> SharedWishlists { get; set; } = new();
    public List<Trip> OwnedTrips { get; set; } = new();
    public List<SharedTrip> SharedTrips { get; set; } = new();
}
