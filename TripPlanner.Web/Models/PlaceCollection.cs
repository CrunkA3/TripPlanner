using System.ComponentModel.DataAnnotations;
using TripPlanner.Web.Data;

namespace TripPlanner.Web.Models;

public class PlaceCollection
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public string OwnerId { get; set; } = string.Empty;

    public ApplicationUser? Owner { get; set; }

    /// <summary>
    /// When set, this collection is publicly accessible via /share/collection/{PublicShareToken}.
    /// Null means the collection is private.
    /// </summary>
    [MaxLength(64)]
    public string? PublicShareToken { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    // Navigation property
    public List<PlaceCollectionItem> Items { get; set; } = [];
}

public class PlaceCollectionItem
{
    [Required]
    [MaxLength(450)]
    public string CollectionId { get; set; } = string.Empty;

    public PlaceCollection? Collection { get; set; }

    [Required]
    [MaxLength(450)]
    public string PlaceId { get; set; } = string.Empty;

    public Place? Place { get; set; }

    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;
}
