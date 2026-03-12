using System.Text.Json.Serialization;

namespace TripPlanner.Web.Services.OpenAI;

/// <summary>
/// Represents a function call request for a tool in the OpenAI API, including the function name and its arguments as a
/// JSON string.
/// </summary>
/// <remarks>This class is used to serialize function call requests when interacting with the OpenAI API's tool
/// calling feature. The arguments must be provided as a JSON-formatted string, as required by the OpenAI API
/// specification.</remarks>
internal sealed class OpenAIRequestToolCallFunction
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;


    // OpenAI requires arguments as a JSON string, not an object.
    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = string.Empty;
}
