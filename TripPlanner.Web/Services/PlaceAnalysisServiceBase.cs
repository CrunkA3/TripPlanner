using System.Net;
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
        // Step 1: Validate the URL and fetch the page content.
        // Use "UrlFetchNoRedirect" (AllowAutoRedirect=false) so every redirect hop can be
        // validated against UrlSecurityHelper before being followed, preventing redirect-based SSRF.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Only http and https URLs are supported.");
        }

        if (UrlSecurityHelper.IsPrivateOrLocalUri(uri))
        {
            throw new InvalidOperationException("Private or local URLs are not allowed.");
        }

        string html;
        string pageContent;
        try
        {
            using var httpClient = _httpClientFactory.CreateClient("UrlFetchNoRedirect");
            var response = await httpClient.GetAsync(uri, cancellationToken);

            // Manually follow up to 5 redirects, validating each Location against SSRF rules
            int redirects = 0;
            while (response.StatusCode is HttpStatusCode.MovedPermanently
                                        or HttpStatusCode.Found
                                        or HttpStatusCode.SeeOther
                                        or HttpStatusCode.TemporaryRedirect
                                        or HttpStatusCode.PermanentRedirect)
            {
                if (++redirects > 5 || response.Headers.Location is not { } location)
                    break;

                var next = location.IsAbsoluteUri ? location : new Uri(uri, location);
                if ((next.Scheme != Uri.UriSchemeHttp && next.Scheme != Uri.UriSchemeHttps)
                    || UrlSecurityHelper.IsPrivateOrLocalUri(next))
                {
                    throw new InvalidOperationException("Redirect target is a private or local URL.");
                }

                uri = next;
                response = await httpClient.GetAsync(uri, cancellationToken);
            }

            response.EnsureSuccessStatusCode();
            html = await response.Content.ReadAsStringAsync(cancellationToken);
            pageContent = PlaceAnalysisHelpers.ExtractTextFromHtml(html, MaxContentLength);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException)
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
        string responseText;
        string prompt;
        try
        {
            (responseText, prompt) = await GetLlmResponseAsync(pageContent, languageTag, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get LLM analysis for URL: {Url}", url);
            throw;
        }

        if (string.IsNullOrWhiteSpace(responseText))
            return null;

        PlaceSuggestion? suggestion;
        try
        {
            suggestion = JsonSerializer.Deserialize<PlaceSuggestion>(responseText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize LLM response for URL: {Url}", url);
            throw new InvalidOperationException("The AI service returned an invalid response.", ex);
        }

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
