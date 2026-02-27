using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TripPlanner.Web.Data;

namespace TripPlanner.Web.Models;

public class Wishlist
{
    [Key, Required]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public List<Place> Places { get; set; } = [];
    public List<UserWishlist> SharedWith { get; set; } = [];
}

public class UserWishlist
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public string WishlistId { get; set; } = string.Empty;
    public Wishlist? Wishlist { get; set; }

    public ShareLevel Level { get; set; }

    public DateTime SharedAt { get; set; } = DateTime.UtcNow;
}
