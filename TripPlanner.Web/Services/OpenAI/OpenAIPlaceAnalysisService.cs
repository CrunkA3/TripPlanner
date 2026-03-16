using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Services.OpenAI;

/// <summary>
/// <see cref="IPlaceAnalysisService"/> implementation backed by the OpenAI Chat Completions API.
/// Registered when <c>AI:Provider</c> is set to <c>OpenAI</c> in configuration.
/// </summary>
public class OpenAIPlaceAnalysisService : IPlaceAnalysisService
{
    // Maximum number of characters of page text sent to the LLM to stay within prompt limits.
    private const int MaxContentLength = 5000;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAIPlaceAnalysisService> _logger;
    private readonly IGeocodingService _geocodingService;
    private readonly BrowserCultureService _browserCultureService;

    public OpenAIPlaceAnalysisService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OpenAIPlaceAnalysisService> logger,
        IGeocodingService geocodingService,
        BrowserCultureService browserCultureService)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _geocodingService = geocodingService;
        _browserCultureService = browserCultureService;
    }

    // SSE chunk from the OpenAI streaming response.
    private sealed class OpenAIStreamChunk
    {
        [System.Text.Json.Serialization.JsonPropertyName("choices")]
        public List<OpenAIStreamChoice> Choices { get; set; } = [];
    }

    private sealed class OpenAIStreamChoice
    {
        [System.Text.Json.Serialization.JsonPropertyName("delta")]
        public OpenAIStreamDelta Delta { get; set; } = new();

        [System.Text.Json.Serialization.JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private sealed class OpenAIStreamDelta
    {
        [System.Text.Json.Serialization.JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    public async Task<PlaceAnalysisResult?> AnalyzeUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        // Step 1: Fetch the page content.
        string pageContent;
        try
        {
            using var httpClient = _httpClientFactory.CreateClient("UrlFetch");
            var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            pageContent = ExtractTextFromHtml(html);
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

        // Step 2: Send to OpenAI for analysis.
        var modelName = _configuration["OpenAI:Model"] ?? "gpt-4o";
        var categories = string.Join(", ", Enum.GetNames<PlaceCategory>());

        var systemPrompt = $"You are a travel assistant that extracts structured place information from web page content. Always write the name, description, and tags in the user's language: {_browserCultureService.LanguageTag}.";
        var userPrompt = $"""
            Analyze the following web page content about a place and extract structured information.

            Return ONLY a valid JSON object (no markdown, no explanation) with these fields:
            - "name": string (the name of the place)
            - "description": string (a brief description in 2-3 sentences)
            - "category": string (one of: {categories})
            - "address": string or null (the full postal address of the place if mentioned on the page, e.g. "Musterstraße 1, 12345 Berlin, Germany")
            - "latitude": number or null (geographic latitude if explicitly mentioned on the page)
            - "longitude": number or null (geographic longitude if explicitly mentioned on the page)
            - "tags": array of strings (2-5 relevant travel tags like "hiking", "family", "outdoor", etc.)

            Web page content:
            {pageContent}
            """;

        var requestBody = new
        {
            model = modelName,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            response_format = new { type = "json_object" },
            stream = true
        };

        try
        {
            var openAIClient = _httpClientFactory.CreateClient("OpenAI");
            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions") { Content = content };
            using var openAIResponse = await openAIClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            openAIResponse.EnsureSuccessStatusCode();

            using var responseStream = await openAIResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(responseStream);

            var chunkOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var responseBuilder = new StringBuilder();

            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (!line.StartsWith("data: ", StringComparison.Ordinal)) continue;

                var data = line["data: ".Length..];
                if (data == "[DONE]") break;

                var chunk = JsonSerializer.Deserialize<OpenAIStreamChunk>(data, chunkOptions);
                var deltaContent = chunk?.Choices.Count > 0 ? chunk.Choices[0].Delta.Content : null;
                if (!string.IsNullOrEmpty(deltaContent))
                    responseBuilder.Append(deltaContent);
            }

            var responseText = responseBuilder.ToString();
            if (string.IsNullOrWhiteSpace(responseText))
                return null;

            var suggestion = JsonSerializer.Deserialize<PlaceSuggestion>(responseText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Step 3: Geocode if the LLM did not return coordinates.
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
                Prompt = userPrompt,
                RawResponse = responseText,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get LLM analysis from OpenAI for URL: {Url}", url);
            throw new InvalidOperationException($"Could not analyze the URL with the AI service: {ex.Message}", ex);
        }
    }

    private static string ExtractTextFromHtml(string html)
    {
        // Remove script, style, and head blocks including their content
        html = Regex.Replace(html, @"<(script|style|head)[^>]*>.*?</(script|style|head)>",
            " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        // Remove all remaining HTML tags
        html = Regex.Replace(html, @"<[^>]+>", " ");

        // Decode HTML entities
        html = System.Net.WebUtility.HtmlDecode(html);

        // Normalize whitespace
        html = Regex.Replace(html, @"\s+", " ").Trim();

        // Truncate to a manageable size for the LLM
        return html.Length > MaxContentLength ? html[..MaxContentLength] : html;
    }
}
