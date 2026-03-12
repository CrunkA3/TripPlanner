using System.Text.Json.Serialization;

namespace TripPlanner.Web.Services.OpenAI;

/// <summary>
/// Represents the function details in an incremental tool call delta.
/// </summary>
internal sealed class OpenAIStreamToolCallFunctionDelta
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("arguments")] public string? Arguments { get; set; }
}
