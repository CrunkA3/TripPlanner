using System.Text;
using System.Text.Json;
using TripPlanner.Web.Models;
using TripPlanner.Web.Services;

namespace TripPlanner.Web.Services.OpenAI;

/// <summary>
/// <see cref="IPlaceAnalysisService"/> implementation backed by the OpenAI Chat Completions API.
/// Registered when <c>AI:Provider</c> is set to <c>OpenAI</c> in configuration.
/// </summary>
public class OpenAIPlaceAnalysisService : PlaceAnalysisServiceBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAIPlaceAnalysisService> _logger;

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

    public OpenAIPlaceAnalysisService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OpenAIPlaceAnalysisService> logger,
        IGeocodingService geocodingService)
        : base(httpClientFactory, logger, geocodingService)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task<(string ResponseText, string Prompt)> GetLlmResponseAsync(
        string pageContent, string languageTag, CancellationToken cancellationToken)
    {
        var modelName = _configuration["OpenAI:Model"] ?? "gpt-4o";
        var categories = string.Join(", ", Enum.GetNames<PlaceCategory>());

        var systemPrompt = $"You are a travel assistant that extracts structured place information from web page content. Always write the name, description, and tags in the user's language: {languageTag}.";
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

            return (responseBuilder.ToString(), userPrompt);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get LLM analysis from OpenAI for URL.");
            throw new InvalidOperationException($"Could not analyze the URL with the AI service: {ex.Message}", ex);
        }
    }
}
