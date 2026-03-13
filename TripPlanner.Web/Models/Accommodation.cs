namespace TripPlanner.Web.Models;

public class Accommodation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TripId { get; set; } = string.Empty;
    public Trip? Trip { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }

    // Planned check-in / check-out
    public DateTimeOffset? PlannedCheckIn { get; set; }
    public DateTimeOffset? PlannedCheckOut { get; set; }

    // Earliest possible times
    public TimeOnly? EarliestCheckIn { get; set; }
    public TimeOnly? LatestCheckOut { get; set; }

    // Location
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public string? Link { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
