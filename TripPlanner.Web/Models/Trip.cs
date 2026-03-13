using TripPlanner.Web.Data;

namespace TripPlanner.Web.Models;

public class Trip
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    
    // Owner
    public string OwnerId { get; set; } = string.Empty;
    public ApplicationUser? Owner { get; set; }
    
    public List<TripDay> Days { get; set; } = new();
    public List<TripPlace> UnscheduledPlaces { get; set; } = new();
    public List<Accommodation> Accommodations { get; set; } = new();
    
    // Sharing
    public List<SharedTrip> SharedWith { get; set; } = new();
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class TripDay
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int DayNumber { get; set; }
    public DateTimeOffset? Date { get; set; }
    public List<TripPlace> Places { get; set; } = new();
}

public class TripPlace
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PlaceId { get; set; } = string.Empty;
    public Place? Place { get; set; }
    public DateTimeOffset? ScheduledTime { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Notes { get; set; }
    public int Order { get; set; }
}
