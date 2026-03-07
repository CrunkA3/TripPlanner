using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TripPlanner.Web.Services;

/// <summary>
/// Geocoding service that uses the free OpenStreetMap Nominatim API.
/// </summary>
public class NominatimGeocodingService(
    IHttpClientFactory httpClientFactory,
    ILogger<NominatimGeocodingService> logger) : IGeocodingService
{
    public async Task<GeocodingResult?> GeocodeAsync(string placeName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(placeName))
            return null;

        try
        {
            var client = httpClientFactory.CreateClient("Nominatim");
            var url = $"search?q={Uri.EscapeDataString(placeName)}&format=json&limit=1";

            var results = await client.GetFromJsonAsync<List<NominatimResult>>(url, cancellationToken);
            if (results is null || results.Count == 0)
            {
                logger.LogDebug("Nominatim returned no results for place: {PlaceName}", placeName);
                return null;
            }

            var first = results[0];
            if (!double.TryParse(first.Lat, System.Globalization.NumberStyles.AllowDecimalPoint | System.Globalization.NumberStyles.AllowLeadingSign,
                    System.Globalization.CultureInfo.InvariantCulture, out var lat) ||
                !double.TryParse(first.Lon, System.Globalization.NumberStyles.AllowDecimalPoint | System.Globalization.NumberStyles.AllowLeadingSign,
                    System.Globalization.CultureInfo.InvariantCulture, out var lon))
            {
                logger.LogWarning("Could not parse coordinates from Nominatim response for place: {PlaceName}", placeName);
                return null;
            }

            logger.LogDebug("Geocoded '{PlaceName}' to {Lat}, {Lon}", placeName, lat, lon);
            return new GeocodingResult(lat, lon);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to geocode place: {PlaceName}", placeName);
            return null;
        }
    }

    private sealed class NominatimResult
    {
        [JsonPropertyName("lat")]
        public string? Lat { get; set; }

        [JsonPropertyName("lon")]
        public string? Lon { get; set; }
    }
}
