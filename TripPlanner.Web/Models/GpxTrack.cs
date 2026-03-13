namespace TripPlanner.Web.Models;

public class GpxTrack
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<GpxPoint> Points { get; set; } = new();
    public double TotalDistance { get; set; }
    public double ElevationGain { get; set; }
    public double ElevationLoss { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
}

public class GpxPoint
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string GpxTrackId { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Elevation { get; set; }
    public DateTimeOffset? Time { get; set; }
    public int Order { get; set; }
}
