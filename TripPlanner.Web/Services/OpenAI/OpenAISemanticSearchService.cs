using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace TripPlanner.Web.Services.OpenAI;

/// <summary>
/// <see cref="ISemanticSearchService"/> implementation backed by the OpenAI Embeddings API
/// (<c>POST /v1/embeddings</c>). Registered when <c>AI:Provider</c> is <c>OpenAI</c>.
/// </summary>
public class OpenAISemanticSearchService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IMemoryCache cache,
    ILogger<OpenAISemanticSearchService> logger)
    : SemanticSearchServiceBase(cache, logger)
{
    private sealed class EmbeddingsRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("input")]
        public string[] Input { get; set; } = [];
    }

    private sealed class EmbeddingsResponse
    {
        [JsonPropertyName("data")]
        public List<EmbeddingData> Data { get; set; } = [];
    }

    private sealed class EmbeddingData
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = [];
    }

    protected override async Task<float[][]?> GetEmbeddingsAsync(string[] texts, CancellationToken ct)
    {
        var model = configuration["OpenAI:EmbeddingsModel"] ?? "text-embedding-3-small";
        try
        {
            var client = httpClientFactory.CreateClient("OpenAI");
            var requestBody = new EmbeddingsRequest { Model = model, Input = texts };
            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await client.PostAsync("/v1/embeddings", content, ct);
            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<EmbeddingsResponse>(responseText);
            if (result?.Data == null) return null;

            // Return embeddings in original input order.
            return result.Data
                .OrderBy(d => d.Index)
                .Select(d => d.Embedding)
                .ToArray();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve embeddings from OpenAI ({Model}).", model);
            return null;
        }
    }
}
