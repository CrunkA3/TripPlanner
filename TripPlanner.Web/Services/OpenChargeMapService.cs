using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TripPlanner.Web.Services;

public class OpenChargeMapService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<OpenChargeMapService> logger)
{
    public async Task<List<ChargingStationSearchResult>> SearchStationsAsync(
        double latitude,
        double longitude,
        double distanceKm = 5,
        int maxResults = 5,
        CancellationToken ct = default)
    {
        distanceKm = Math.Clamp(distanceKm, 1, 50);
        maxResults = Math.Clamp(maxResults, 1, 20);

        try
        {
            var client = httpClientFactory.CreateClient("OpenChargeMap");
            var key = configuration["OpenChargeMap:ApiKey"];

            var url = $"/v3/poi/?output=json" +
                      $"&latitude={latitude.ToString(CultureInfo.InvariantCulture)}" +
                      $"&longitude={longitude.ToString(CultureInfo.InvariantCulture)}" +
                      $"&distance={distanceKm.ToString("F1", CultureInfo.InvariantCulture)}" +
                      $"&distanceunit=KM" +
                      $"&maxresults={maxResults}" +
                      "&compact=true&verbose=false";

            if (!string.IsNullOrWhiteSpace(key))
                url += $"&key={Uri.EscapeDataString(key)}";

            var response = await client.GetFromJsonAsync<List<OpenChargeMapPoi>>(url, ct);
            if (response is null || response.Count == 0)
                return [];

            return [.. response.Select(MapResult)];
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load charging stations around {Latitude}, {Longitude}", latitude, longitude);
            return [];
        }
    }

    private static ChargingStationSearchResult MapResult(OpenChargeMapPoi poi)
    {
        var addressParts = new[]
        {
            poi.AddressInfo?.AddressLine1,
            poi.AddressInfo?.Town,
            poi.AddressInfo?.StateOrProvince,
            poi.AddressInfo?.Postcode
        }.Where(p => !string.IsNullOrWhiteSpace(p));

        var connectionTypes = poi.Connections?
            .Select(c => c.ConnectionType?.Title)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        var maxPowerKw = poi.Connections?
            .Where(c => c.PowerKW.HasValue)
            .Select(c => c.PowerKW!.Value)
            .DefaultIfEmpty()
            .Max();

        return new ChargingStationSearchResult(
            Name: poi.AddressInfo?.Title ?? "Charging Station",
            Address: string.Join(", ", addressParts),
            Latitude: poi.AddressInfo?.Latitude,
            Longitude: poi.AddressInfo?.Longitude,
            DistanceKm: poi.AddressInfo?.Distance,
            NumberOfPoints: poi.NumberOfPoints,
            MaxPowerKw: maxPowerKw > 0 ? maxPowerKw : null,
            ConnectionTypes: connectionTypes);
    }

    private sealed class OpenChargeMapPoi
    {
        [JsonPropertyName("AddressInfo")] public OpenChargeMapAddressInfo? AddressInfo { get; set; }
        [JsonPropertyName("NumberOfPoints")] public int? NumberOfPoints { get; set; }
        [JsonPropertyName("Connections")] public List<OpenChargeMapConnection>? Connections { get; set; }
    }

    private sealed class OpenChargeMapAddressInfo
    {
        [JsonPropertyName("Title")] public string? Title { get; set; }
        [JsonPropertyName("AddressLine1")] public string? AddressLine1 { get; set; }
        [JsonPropertyName("Town")] public string? Town { get; set; }
        [JsonPropertyName("StateOrProvince")] public string? StateOrProvince { get; set; }
        [JsonPropertyName("Postcode")] public string? Postcode { get; set; }
        [JsonPropertyName("Latitude")] public double? Latitude { get; set; }
        [JsonPropertyName("Longitude")] public double? Longitude { get; set; }
        [JsonPropertyName("Distance")] public double? Distance { get; set; }
    }

    private sealed class OpenChargeMapConnection
    {
        [JsonPropertyName("PowerKW")] public double? PowerKW { get; set; }
        [JsonPropertyName("ConnectionType")] public OpenChargeMapConnectionType? ConnectionType { get; set; }
    }

    private sealed class OpenChargeMapConnectionType
    {
        [JsonPropertyName("Title")] public string? Title { get; set; }
    }
}

public record ChargingStationSearchResult(
    string Name,
    string Address,
    double? Latitude,
    double? Longitude,
    double? DistanceKm,
    int? NumberOfPoints,
    double? MaxPowerKw,
    List<string> ConnectionTypes);
