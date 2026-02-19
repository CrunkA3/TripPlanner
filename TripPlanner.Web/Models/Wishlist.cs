using TripPlanner.Web.Data;

namespace TripPlanner.Web.Models;

public class Wishlist
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    // Owner
    public string OwnerId { get; set; } = string.Empty;
    public ApplicationUser? Owner { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public List<Place> Places { get; set; } = new();
    public List<UserWishlist> SharedWith { get; set; } = new();
}

public class UserWishlist
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    
    public string WishlistId { get; set; } = string.Empty;
    public Wishlist? Wishlist { get; set; }
    
    public DateTime SharedAt { get; set; } = DateTime.UtcNow;
}
