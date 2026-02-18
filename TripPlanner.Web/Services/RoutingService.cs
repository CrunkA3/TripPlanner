using TripPlanner.Web.Models;

namespace TripPlanner.Web.Services;

public class RoutingService
{
    private readonly GpxService _gpxService;

    public RoutingService(GpxService gpxService)
    {
        _gpxService = gpxService;
    }

    public double CalculateDistance(Place from, Place to)
    {
        return _gpxService.CalculateDistance(from.Latitude, from.Longitude, to.Latitude, to.Longitude);
    }

    public int EstimateTravelTime(Place from, Place to, double speedKmh = 50)
    {
        var distance = CalculateDistance(from, to);
        return (int)Math.Ceiling((distance / speedKmh) * 60); // Return minutes
    }

    public TripDayAnalysis AnalyzeTripDay(TripDay day, List<Place> places)
    {
        var analysis = new TripDayAnalysis
        {
            DayNumber = day.DayNumber,
            Date = day.Date
        };

        if (!day.Places.Any())
            return analysis;

        var scheduledPlaces = day.Places
            .Where(tp => tp.ScheduledTime.HasValue)
            .OrderBy(tp => tp.ScheduledTime)
            .ToList();

        analysis.TotalScheduledMinutes = scheduledPlaces
            .Where(tp => tp.DurationMinutes.HasValue)
            .Sum(tp => tp.DurationMinutes!.Value);

        // Calculate travel times between places
        for (int i = 1; i < scheduledPlaces.Count; i++)
        {
            var prevPlace = places.FirstOrDefault(p => p.Id == scheduledPlaces[i - 1].PlaceId);
            var currPlace = places.FirstOrDefault(p => p.Id == scheduledPlaces[i].PlaceId);

            if (prevPlace != null && currPlace != null)
            {
                var travelTime = EstimateTravelTime(prevPlace, currPlace);
                analysis.TotalTravelMinutes += travelTime;
                analysis.TravelSegments.Add(new TravelSegment
                {
                    FromPlaceName = prevPlace.Name,
                    ToPlaceName = currPlace.Name,
                    DistanceKm = CalculateDistance(prevPlace, currPlace),
                    TravelMinutes = travelTime
                });
            }
        }

        analysis.TotalMinutes = analysis.TotalScheduledMinutes + analysis.TotalTravelMinutes;

        // Check for scheduling conflicts
        for (int i = 1; i < scheduledPlaces.Count; i++)
        {
            var prev = scheduledPlaces[i - 1];
            var curr = scheduledPlaces[i];

            if (prev.ScheduledTime.HasValue && curr.ScheduledTime.HasValue)
            {
                var prevEnd = prev.ScheduledTime.Value.AddMinutes(prev.DurationMinutes ?? 0);
                var travelTime = analysis.TravelSegments.ElementAtOrDefault(i - 1)?.TravelMinutes ?? 0;
                var requiredStart = prevEnd.AddMinutes(travelTime);

                if (curr.ScheduledTime.Value < requiredStart)
                {
                    analysis.HasConflicts = true;
                    analysis.Warnings.Add($"Conflict: Not enough time between {prev.Place?.Name ?? "Place"} and {curr.Place?.Name ?? "Place"}");
                }
            }
        }

        // Check if day is too packed (more than 14 hours)
        if (analysis.TotalMinutes > 840)
        {
            analysis.Warnings.Add($"Warning: Day is very long ({analysis.TotalMinutes / 60:F1} hours). Consider reducing activities.");
        }

        return analysis;
    }
}

public class TripDayAnalysis
{
    public int DayNumber { get; set; }
    public DateTime? Date { get; set; }
    public int TotalScheduledMinutes { get; set; }
    public int TotalTravelMinutes { get; set; }
    public int TotalMinutes { get; set; }
    public List<TravelSegment> TravelSegments { get; set; } = new();
    public bool HasConflicts { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class TravelSegment
{
    public string FromPlaceName { get; set; } = string.Empty;
    public string ToPlaceName { get; set; } = string.Empty;
    public double DistanceKm { get; set; }
    public int TravelMinutes { get; set; }
}
