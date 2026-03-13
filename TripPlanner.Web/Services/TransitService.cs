using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TripPlanner.Web.Services;

/// <summary>
/// Service for searching public-transit (ÖPNV) connections using the free
/// Deutsche Bahn transport.rest API (https://v6.db.transport.rest).
/// No API key is required.
/// </summary>
public class TransitService(IHttpClientFactory httpClientFactory, ILogger<TransitService> logger)
{
    private readonly ConcurrentDictionary<string, TransitStop?> _stopCache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Looks up a stop/station by name and returns its Hafas ID and display name.</summary>
    public async Task<TransitStop?> FindStopAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var cacheKey = name.Trim();
        if (_stopCache.TryGetValue(cacheKey, out var cached))
            return cached;

        try
        {
            var client = httpClientFactory.CreateClient("DbTransit");
            var url = $"locations?query={Uri.EscapeDataString(name)}&results=1&stops=true&fuzzy=true";
            var results = await client.GetFromJsonAsync<List<DbTransitLocation>>(url, ct);

            var first = results?.FirstOrDefault(r => r.Type is "stop" or "station");
            if (first?.Id is null || first.Name is null)
            {
                _stopCache[cacheKey] = null;
                return null;
            }

            var stop = new TransitStop(first.Id, first.Name);
            _stopCache[cacheKey] = stop;
            return stop;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to find transit stop for query: {Name}", name);
            return null;
        }
    }

    /// <summary>Searches for transit connections between two stops.</summary>
    public async Task<List<TransitJourney>> SearchJourneysAsync(
        string fromId, string toId, DateTimeOffset departure, int results = 3, CancellationToken ct = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("DbTransit");
            var dep = departure.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ",
                System.Globalization.CultureInfo.InvariantCulture);
            var url = $"journeys?from={Uri.EscapeDataString(fromId)}" +
                      $"&to={Uri.EscapeDataString(toId)}" +
                      $"&departure={Uri.EscapeDataString(dep)}" +
                      $"&results={results}&language=de";

            var response = await client.GetFromJsonAsync<DbJourneysResponse>(url, ct);
            if (response?.Journeys is null || response.Journeys.Count == 0)
                return [];

            var journeys = new List<TransitJourney>();
            foreach (var j in response.Journeys)
            {
                if (j.Legs is null || j.Legs.Count == 0) continue;

                var firstLeg = j.Legs[0];
                var lastLeg = j.Legs[^1];

                var depTime = firstLeg.Departure ?? firstLeg.PlannedDeparture;
                var arrTime = lastLeg.Arrival ?? lastLeg.PlannedArrival;
                if (depTime is null || arrTime is null) continue;

                var lines = j.Legs
                    .Where(l => l.Line is not null)
                    .Select(l => l.Line!.Name ?? l.Line.ProductName ?? "?")
                    .Distinct()
                    .ToList();

                var transfers = Math.Max(0, j.Legs.Count(l => l.Line is not null) - 1);

                journeys.Add(new TransitJourney(
                    Departure: depTime.Value,
                    Arrival: arrTime.Value,
                    Duration: arrTime.Value - depTime.Value,
                    Transfers: transfers,
                    Lines: lines));
            }

            return journeys;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to search transit journeys from {From} to {To}", fromId, toId);
            return [];
        }
    }

    // ── Response models ───────────────────────────────────────────────────────────

    private sealed class DbTransitLocation
    {
        [JsonPropertyName("type")] public string? Type { get; set; }
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
    }

    private sealed class DbJourneysResponse
    {
        [JsonPropertyName("journeys")] public List<DbJourney>? Journeys { get; set; }
    }

    private sealed class DbJourney
    {
        [JsonPropertyName("legs")] public List<DbLeg>? Legs { get; set; }
    }

    private sealed class DbLeg
    {
        [JsonPropertyName("departure")] public DateTimeOffset? Departure { get; set; }
        [JsonPropertyName("plannedDeparture")] public DateTimeOffset? PlannedDeparture { get; set; }
        [JsonPropertyName("arrival")] public DateTimeOffset? Arrival { get; set; }
        [JsonPropertyName("plannedArrival")] public DateTimeOffset? PlannedArrival { get; set; }
        [JsonPropertyName("line")] public DbLine? Line { get; set; }
    }

    private sealed class DbLine
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("productName")] public string? ProductName { get; set; }
    }
}

public record TransitStop(string Id, string Name);

public record TransitJourney(
    DateTimeOffset Departure,
    DateTimeOffset Arrival,
    TimeSpan Duration,
    int Transfers,
    List<string> Lines);
