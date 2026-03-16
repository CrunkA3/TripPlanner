using System.ComponentModel.DataAnnotations;

namespace TripPlanner.Web.Models;

public class ChatJob
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>The conversation this job belongs to.</summary>
    [Required]
    public string ConversationId { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>The user message to process.</summary>
    [Required]
    public string UserMessage { get; set; } = string.Empty;

    public ChatJobStatus Status { get; set; } = ChatJobStatus.Pending;

    public string? ErrorMessage { get; set; }

    /// <summary>BCP 47 language tag captured from the browser at job creation time (e.g. "de", "en-US").</summary>
    public string LanguageTag { get; set; } = "en";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}
