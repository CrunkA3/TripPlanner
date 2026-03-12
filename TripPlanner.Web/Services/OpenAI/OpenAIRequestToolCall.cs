using System.Text.Json.Serialization;

namespace TripPlanner.Web.Services.OpenAI;

/// <summary>
/// Represents a tool call request sent to the OpenAI API, including the tool's identifier, type, and function details.
/// </summary>
/// <remarks>This class is typically used to serialize or deserialize tool call requests when interacting with the
/// OpenAI API. The properties correspond to the expected JSON structure for tool calls in OpenAI's API
/// requests.</remarks>
internal sealed class OpenAIRequestToolCall
{
    /// <summary>
    /// Gets or sets the unique identifier for the entity.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of the function as represented in the JSON payload.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    /// <summary>
    /// Gets or sets the function call details for the OpenAI tool request.
    /// </summary>
    [JsonPropertyName("function")]
    public OpenAIRequestToolCallFunction Function { get; set; } = new();
}
