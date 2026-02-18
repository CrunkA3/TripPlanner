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
    public DateTime? CreatedAt { get; set; }
}

public class GpxPoint
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Elevation { get; set; }
    public DateTime? Time { get; set; }
}
