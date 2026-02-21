using TripPlanner.Web.Data;

namespace TripPlanner.Web.Models;

public class Trip
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    
    // Owner
    public string OwnerId { get; set; } = string.Empty;
    public ApplicationUser? Owner { get; set; }
    
    public List<TripDay> Days { get; set; } = new();
    public List<TripPlace> UnscheduledPlaces { get; set; } = new();
    public List<Accommodation> Accommodations { get; set; } = new();
    
    // Sharing
    public List<SharedTrip> SharedWith { get; set; } = new();
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class TripDay
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int DayNumber { get; set; }
    public DateTime? Date { get; set; }
    public List<TripPlace> Places { get; set; } = new();
}

public class TripPlace
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PlaceId { get; set; } = string.Empty;
    public Place? Place { get; set; }
    public DateTime? ScheduledTime { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Notes { get; set; }
    public int Order { get; set; }
}
