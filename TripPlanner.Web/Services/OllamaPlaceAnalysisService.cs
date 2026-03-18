using System.Text;
using System.Text.Json;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Services;

public class OllamaPlaceAnalysisService : PlaceAnalysisServiceBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<OllamaPlaceAnalysisService> _logger;

    private sealed record OllamaStreamChunk(string? Response, bool Done);

    public OllamaPlaceAnalysisService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OllamaPlaceAnalysisService> logger,
        IGeocodingService geocodingService)
        : base(httpClientFactory, logger, geocodingService)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task<(string ResponseText, string Prompt)> GetLlmResponseAsync(
        string pageContent, string languageTag, CancellationToken cancellationToken)
    {
        var modelName = _configuration["Ollama:Model"] ?? "llama3.2";
        var categories = string.Join(", ", Enum.GetNames<PlaceCategory>());

        var prompt = $"""
            You are a travel assistant. Analyze the following web page content about a place and extract structured information.
            Always write the name, description, and tags in the user's language: {languageTag}.

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
            prompt,
            stream = true,
            format = "json"
        };

        try
        {
            var ollamaClient = _httpClientFactory.CreateClient("Ollama");
            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Use streaming so Ollama sends tokens incrementally, keeping the connection alive
            // and avoiding TCP keep-alive/proxy timeouts that occur with stream=false.
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/generate") { Content = content };
            using var ollamaResponse = await ollamaClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            ollamaResponse.EnsureSuccessStatusCode();

            using var responseStream = await ollamaResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(responseStream);

            var streamOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var responseBuilder = new StringBuilder();
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var chunk = JsonSerializer.Deserialize<OllamaStreamChunk>(line, streamOptions);
                if (!string.IsNullOrEmpty(chunk?.Response))
                    responseBuilder.Append(chunk.Response);

                if (chunk?.Done == true)
                    break;
            }

            return (responseBuilder.ToString(), prompt);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Could not analyze the URL with the local LLM: {ex.Message}", ex);
        }
    }
}
