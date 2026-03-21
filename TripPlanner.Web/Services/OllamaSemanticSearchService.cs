using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace TripPlanner.Web.Services;

/// <summary>
/// <see cref="ISemanticSearchService"/> implementation backed by the Ollama Embeddings API
/// (<c>POST /api/embed</c>). Registered when <c>AI:Provider</c> is <c>Ollama</c>.
/// </summary>
public class OllamaSemanticSearchService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IMemoryCache cache,
    ILogger<OllamaSemanticSearchService> logger)
    : SemanticSearchServiceBase(cache, logger)
{
    // Ollama /api/embed (v2+) supports batched input.
    private sealed class EmbedRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("input")]
        public string[] Input { get; set; } = [];
    }

    private sealed class EmbedResponse
    {
        [JsonPropertyName("embeddings")]
        public float[][]? Embeddings { get; set; }
    }

    protected override async Task<float[][]?> GetEmbeddingsAsync(string[] texts, CancellationToken ct)
    {
        var model = configuration["Ollama:EmbeddingsModel"] ?? "nomic-embed-text";
        try
        {
            var client = httpClientFactory.CreateClient("Ollama");
            var requestBody = new EmbedRequest { Model = model, Input = texts };
            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await client.PostAsync("/api/embed", content, ct);
            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<EmbedResponse>(responseText);
            return result?.Embeddings;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve embeddings from Ollama ({Model}).", model);
            return null;
        }
    }
}
