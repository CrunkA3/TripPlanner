namespace TripPlanner.Web.Services;

public interface IGeocodingService
{
    /// <summary>
    /// Geocodes a place name and returns its coordinates, or null if not found.
    /// </summary>
    Task<GeocodingResult?> GeocodeAsync(string placeName, CancellationToken cancellationToken = default);
}

public record GeocodingResult(double Latitude, double Longitude);
