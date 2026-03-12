using System.Text.Json.Serialization;

namespace TripPlanner.Web.Services.OpenAI;

/// <summary>
/// Represents an incremental tool call delta in an OpenAI SSE streaming response.
/// </summary>
internal sealed class OpenAIStreamToolCallDelta
{
    [JsonPropertyName("index")] public int Index { get; set; }
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("function")] public OpenAIStreamToolCallFunctionDelta? Function { get; set; }
}
