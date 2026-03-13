using System.ComponentModel.DataAnnotations;

namespace TripPlanner.Web.Models;

public class ChatConversation
{
    [Key, Required]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = "New Conversation";

    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    // Navigation properties
    public List<ChatMessage> Messages { get; set; } = [];
}

public class ChatMessage
{
    [Key, Required]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string ConversationId { get; set; } = string.Empty;
    public ChatConversation? Conversation { get; set; }

    [Required, MaxLength(50)]
    public string Role { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>Serialized JSON of tool calls for assistant messages that request tool execution. Null for other messages.</summary>
    public string? ToolCallsJson { get; set; }

    /// <summary>For "tool" role messages: the tool-call ID (from OpenAI) that this result satisfies. Null for Ollama-originated messages.</summary>
    public string? ToolCallId { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
