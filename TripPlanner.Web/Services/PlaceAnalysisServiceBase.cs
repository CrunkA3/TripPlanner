using System.Text.Json;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Services;

/// <summary>
/// Abstract base class for <see cref="IPlaceAnalysisService"/> implementations.
/// Handles the shared steps of URL fetching and geocoding fallback so that
/// concrete implementations only need to provide the LLM-specific call.
/// </summary>
public abstract class PlaceAnalysisServiceBase : IPlaceAnalysisService
{
    // Maximum number of characters of page text sent to the LLM to stay within prompt limits.
    protected const int MaxContentLength = 5000;

    private readonly ILogger _logger;
    private readonly IGeocodingService _geocodingService;
    protected readonly IHttpClientFactory _httpClientFactory;

    protected PlaceAnalysisServiceBase(
        IHttpClientFactory httpClientFactory,
        ILogger logger,
        IGeocodingService geocodingService)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _geocodingService = geocodingService;
    }

    /// <summary>
    /// Sends the extracted page content to the LLM and returns the raw JSON response text
    /// together with the prompt that was used, so it can be stored for debugging.
    /// </summary>
    protected abstract Task<(string ResponseText, string Prompt)> GetLlmResponseAsync(
        string pageContent, string languageTag, CancellationToken cancellationToken);

    public async Task<PlaceAnalysisResult?> AnalyzeUrlAsync(string url, string languageTag = "en", CancellationToken cancellationToken = default)
    {
        // Step 1: Fetch the page content.
        string html;
        string pageContent;
        try
        {
            using var httpClient = _httpClientFactory.CreateClient("UrlFetch");
            var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            html = await response.Content.ReadAsStringAsync(cancellationToken);
            pageContent = PlaceAnalysisHelpers.ExtractTextFromHtml(html, MaxContentLength);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch URL: {Url}", url);
            throw new InvalidOperationException($"Could not fetch the URL: {ex.Message}", ex);
        }

        var gpxFileUrls = PlaceAnalysisHelpers.ExtractGpxUrls(html, url);

        // Step 2: Delegate to the subclass for the LLM-specific call.
        var (responseText, prompt) = await GetLlmResponseAsync(pageContent, languageTag, cancellationToken);

        if (string.IsNullOrWhiteSpace(responseText))
            return null;

        var suggestion = JsonSerializer.Deserialize<PlaceSuggestion>(responseText, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Step 3: If the LLM did not return coordinates, geocode using the address found on the page.
        // If no address was found either, fall back to the place name.
        if (suggestion != null && (!suggestion.Latitude.HasValue || !suggestion.Longitude.HasValue))
        {
            var hasAddress = !string.IsNullOrWhiteSpace(suggestion.Address);
            var geocodeQuery = hasAddress ? suggestion.Address : suggestion.Name;

            if (!string.IsNullOrWhiteSpace(geocodeQuery))
            {
                _logger.LogDebug(
                    "LLM did not return coordinates, geocoding using {Source}: '{Query}'.",
                    hasAddress ? "address" : "place name",
                    geocodeQuery);
                var geoResult = await _geocodingService.GeocodeAsync(geocodeQuery, cancellationToken);
                if (geoResult != null)
                {
                    suggestion.Latitude = geoResult.Latitude;
                    suggestion.Longitude = geoResult.Longitude;
                }
            }
        }

        return new PlaceAnalysisResult
        {
            Suggestion = suggestion,
            Prompt = prompt,
            RawResponse = responseText,
            GpxFileUrls = gpxFileUrls,
        };
    }
}
