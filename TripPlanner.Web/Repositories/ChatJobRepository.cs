using Microsoft.EntityFrameworkCore;
using TripPlanner.Web.Data;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public class ChatJobRepository(ApplicationDbContext context) : IChatJobRepository
{
    public async Task<ChatJob> CreateAsync(string conversationId, string userId, string userMessage)
    {
        var job = new ChatJob
        {
            ConversationId = conversationId,
            UserId = userId,
            UserMessage = userMessage
        };
        context.ChatJobs.Add(job);
        await context.SaveChangesAsync();
        return job;
    }

    public Task<ChatJob?> GetByIdAsync(string id, string userId) =>
        context.ChatJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId);

    public Task<ChatJob?> GetActiveJobForConversationAsync(string conversationId, string userId) =>
        context.ChatJobs
            .Where(j => j.ConversationId == conversationId && j.UserId == userId
                        && (j.Status == ChatJobStatus.Pending || j.Status == ChatJobStatus.Processing))
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync();

    public Task<List<ChatJob>> GetPendingJobsAsync(int maxCount = 5) =>
        context.ChatJobs
            .Where(j => j.Status == ChatJobStatus.Pending)
            .OrderBy(j => j.CreatedAt)
            .Take(maxCount)
            .ToListAsync();

    public async Task UpdateAsync(ChatJob job)
    {
        context.ChatJobs.Update(job);
        await context.SaveChangesAsync();
    }
}
