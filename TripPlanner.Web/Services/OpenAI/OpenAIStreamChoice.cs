using System.Text.Json.Serialization;

namespace TripPlanner.Web.Services.OpenAI;

/// <summary>
/// Represents a choice in an OpenAI SSE streaming response chunk.
/// </summary>
internal sealed class OpenAIStreamChoice
{
    [JsonPropertyName("delta")] public OpenAIStreamDelta Delta { get; set; } = new();
    [JsonPropertyName("finish_reason")] public string? FinishReason { get; set; }
}
