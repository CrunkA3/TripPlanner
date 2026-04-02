using Microsoft.EntityFrameworkCore;
using TripPlanner.Web.Data;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public class ChatJobRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : IChatJobRepository
{
    public async Task<ChatJob> CreateAsync(string conversationId, string userId, string userMessage, string languageTag = "en")
    {
        await using var context = contextFactory.CreateDbContext();
        var job = new ChatJob
        {
            ConversationId = conversationId,
            UserId = userId,
            UserMessage = userMessage,
            LanguageTag = languageTag
        };
        context.ChatJobs.Add(job);
        await context.SaveChangesAsync();
        return job;
    }

    public async Task<ChatJob?> GetByIdAsync(string id, string userId)
    {
        await using var context = contextFactory.CreateDbContext();
        return await context.ChatJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId);
    }

    public async Task<ChatJob?> GetActiveJobForConversationAsync(string conversationId, string userId)
    {
        await using var context = contextFactory.CreateDbContext();
        return await context.ChatJobs
            .Where(j => j.ConversationId == conversationId && j.UserId == userId
                        && (j.Status == ChatJobStatus.Pending || j.Status == ChatJobStatus.Processing))
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<List<ChatJob>> GetPendingJobsAsync(int maxCount = 5)
    {
        await using var context = contextFactory.CreateDbContext();
        return await context.ChatJobs
            .Where(j => j.Status == ChatJobStatus.Pending)
            .OrderBy(j => j.CreatedAt)
            .Take(maxCount)
            .ToListAsync();
    }

    public async Task UpdateAsync(ChatJob job)
    {
        await using var context = contextFactory.CreateDbContext();
        context.ChatJobs.Update(job);
        await context.SaveChangesAsync();
    }
}
