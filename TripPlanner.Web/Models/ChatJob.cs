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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
