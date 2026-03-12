using System.Text.Json.Serialization;

namespace TripPlanner.Web.Services.OpenAI;

/// <summary>
/// Represents the delta (incremental content) in an OpenAI SSE streaming response.
/// </summary>
internal sealed class OpenAIStreamDelta
{
    [JsonPropertyName("role")] public string? Role { get; set; }
    [JsonPropertyName("content")] public string? Content { get; set; }
    [JsonPropertyName("tool_calls")] public List<OpenAIStreamToolCallDelta>? ToolCalls { get; set; }
}
