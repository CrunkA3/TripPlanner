using Microsoft.EntityFrameworkCore;
using TripPlanner.Web.Data;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public class ChatConversationRepository(ApplicationDbContext context) : IChatConversationRepository
{
    public Task<List<ChatConversation>> GetByUserAsync(string userId) =>
        context.ChatConversations
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .ToListAsync();

    public Task<ChatConversation?> GetByIdAsync(string id, string userId) =>
        context.ChatConversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

    public async Task<ChatConversation> CreateAsync(string userId, string title)
    {
        var conversation = new ChatConversation
        {
            UserId = userId,
            Title = title,
            CreatedAt = DateTime.UtcNow
        };
        context.ChatConversations.Add(conversation);
        await context.SaveChangesAsync();
        return conversation;
    }

    public async Task UpdateTitleAsync(string id, string title, string userId)
    {
        await context.ChatConversations
            .Where(c => c.Id == id && c.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Title, title));
    }

    public async Task AddMessageAsync(string conversationId, string role, string content, string userId, string? toolCallsJson = null, string? toolCallId = null)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        var now = DateTime.UtcNow;

        // Use the affected-row count to both validate ownership and advance UpdatedAt
        // in a single round-trip, eliminating the separate AnyAsync check.
        var updated = await context.ChatConversations
            .Where(c => c.Id == conversationId && c.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UpdatedAt, now));

        if (updated == 0)
            throw new InvalidOperationException("Chat conversation does not exist or is not owned by the specified user.");

        context.ChatMessages.Add(new ChatMessage
        {
            ConversationId = conversationId,
            Role = role,
            Content = content,
            ToolCallsJson = toolCallsJson,
            ToolCallId = toolCallId,
            CreatedAt = now
        });
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task DeleteAsync(string id, string userId)
    {
        await context.ChatConversations
            .Where(c => c.Id == id && c.UserId == userId)
            .ExecuteDeleteAsync();
    }

    public async Task TouchAsync(string id, string userId)
    {
        await context.ChatConversations
            .Where(c => c.Id == id && c.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UpdatedAt, DateTime.UtcNow));
    }
}
