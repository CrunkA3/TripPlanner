using System.Text.Json.Serialization;

namespace TripPlanner.Web.Services.OpenAI;

/// <summary>
/// Represents a single chunk in an OpenAI SSE streaming response.
/// </summary>
internal sealed class OpenAIStreamChunk
{
    [JsonPropertyName("choices")] public List<OpenAIStreamChoice> Choices { get; set; } = [];
}
