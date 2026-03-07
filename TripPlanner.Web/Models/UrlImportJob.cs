using System.ComponentModel.DataAnnotations;

namespace TripPlanner.Web.Models;

public class UrlImportJob
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string WishlistId { get; set; } = string.Empty;
    public Wishlist? Wishlist { get; set; }

    [Required]
    public string Url { get; set; } = string.Empty;

    public UrlImportJobStatus Status { get; set; } = UrlImportJobStatus.Pending;

    public string? ErrorMessage { get; set; }

    /// <summary>The ID of the Place created by this job, once completed.</summary>
    public string? CreatedPlaceId { get; set; }

    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}
