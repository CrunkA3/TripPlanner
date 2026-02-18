using Microsoft.AspNetCore.Identity;

namespace TripPlanner.Web.Models;

public class User : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public List<Wishlist> Wishlists { get; set; } = new();
    public List<UserWishlist> SharedWishlists { get; set; } = new();
    public List<Trip> OwnedTrips { get; set; } = new();
    public List<SharedTrip> SharedTrips { get; set; } = new();
}
