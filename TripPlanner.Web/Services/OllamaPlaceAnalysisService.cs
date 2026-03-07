using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Services;

public class OllamaPlaceAnalysisService : IPlaceAnalysisService
{
    // Maximum number of characters of page text sent to the LLM to stay within prompt limits
    private const int MaxContentLength = 5000;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OllamaPlaceAnalysisService> _logger;

    public OllamaPlaceAnalysisService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OllamaPlaceAnalysisService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PlaceSuggestion?> AnalyzeUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        // Step 1: Fetch the page content
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

        // Step 2: Send to Ollama for analysis
        var modelName = _configuration["Ollama:Model"] ?? "llama3.2";
        var categories = string.Join(", ", Enum.GetNames<PlaceCategory>());

        var prompt = $"""
            You are a travel assistant. Analyze the following web page content about a place and extract structured information.

            Return ONLY a valid JSON object (no markdown, no explanation) with these fields:
            - "name": string (the name of the place)
            - "description": string (a brief description in 2-3 sentences)
            - "category": string (one of: {categories})
            - "latitude": number or null (geographic latitude if mentioned on the page)
            - "longitude": number or null (geographic longitude if mentioned on the page)
            - "tags": array of strings (2-5 relevant travel tags like "hiking", "family", "outdoor", etc.)

            Web page content:
            {pageContent}
            """;

        var requestBody = new
        {
            model = modelName,
            prompt,
            stream = false,
            format = "json"
        };

        try
        {
            var ollamaClient = _httpClientFactory.CreateClient("Ollama");
            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            // ohne cancellation token, da sonst nach 30 Sekunden abgebrochen wird
            var ollamaResponse = await ollamaClient.PostAsync("/api/generate", content);
            ollamaResponse.EnsureSuccessStatusCode();

            var responseJson = await ollamaResponse.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);

            if (!doc.RootElement.TryGetProperty("response", out var responseElement))
                return null;

            var responseText = responseElement.GetString();
            if (string.IsNullOrWhiteSpace(responseText))
                return null;

            var suggestion = JsonSerializer.Deserialize<PlaceSuggestion>(responseText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return suggestion;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get LLM analysis from Ollama for URL: {Url}", url);
            throw new InvalidOperationException($"Could not analyze the URL with the local LLM: {ex.Message}", ex);
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
