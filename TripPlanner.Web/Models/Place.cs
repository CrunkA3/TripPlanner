namespace TripPlanner.Web.Models;

public class Place
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PlaceCategory Category { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? GpxTrackId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Computed property
    public bool HasGpxTrack => !string.IsNullOrEmpty(GpxTrackId);
}
