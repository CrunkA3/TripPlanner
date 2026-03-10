using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public interface IChatConversationRepository
{
    Task<List<ChatConversation>> GetByUserAsync(string userId);
    Task<ChatConversation?> GetByIdAsync(string id, string userId);
    Task<ChatConversation> CreateAsync(string userId, string title);
    Task UpdateTitleAsync(string id, string title, string userId);
    Task AddMessageAsync(string conversationId, string role, string content, string userId, string? toolCallsJson = null, string? toolCallId = null);
    Task DeleteAsync(string id, string userId);
    Task TouchAsync(string id, string userId);
}
