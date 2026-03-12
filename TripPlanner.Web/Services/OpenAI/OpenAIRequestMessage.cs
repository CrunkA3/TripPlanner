using System.Text.Json.Serialization;

namespace TripPlanner.Web.Services.OpenAI;

internal sealed class OpenAIRequestMessage
{
    /// <summary>
    /// Gets or sets the role associated with the user or entity.
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the textual content associated with this instance.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>
    /// Gets or sets the collection of tool calls associated with the request.
    /// </summary>
    /// <remarks>Each tool call in the collection represents an invocation of a tool as part of the
    /// request. The property may be null if no tool calls are present.</remarks>
    [JsonPropertyName("tool_calls"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<OpenAIRequestToolCall>? ToolCalls { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the tool call associated with this object.
    /// </summary>
    [JsonPropertyName("tool_call_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolCallId { get; set; }
}
