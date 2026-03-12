using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public interface IChatJobRepository
{
    Task<ChatJob> CreateAsync(string conversationId, string userId, string userMessage);
    Task<ChatJob?> GetByIdAsync(string id, string userId);
    Task<ChatJob?> GetActiveJobForConversationAsync(string conversationId, string userId);
    Task<List<ChatJob>> GetPendingJobsAsync(int maxCount = 5);
    Task UpdateAsync(ChatJob job);
}
