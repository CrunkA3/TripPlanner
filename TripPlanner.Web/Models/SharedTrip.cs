using TripPlanner.Web.Data;

namespace TripPlanner.Web.Models;

public class SharedTrip
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    
    public string TripId { get; set; } = string.Empty;
    public Trip? Trip { get; set; }
    
    public DateTime SharedAt { get; set; } = DateTime.UtcNow;
}
