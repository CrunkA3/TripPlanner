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

    public async Task AddMessageAsync(string conversationId, string role, string content, string userId)
    {
        var conversationExists = await context.ChatConversations
            .AnyAsync(c => c.Id == conversationId && c.UserId == userId);
        if (!conversationExists)
            return;

        var message = new ChatMessage
        {
            ConversationId = conversationId,
            Role = role,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };
        context.ChatMessages.Add(message);
        // Update conversation's UpdatedAt timestamp
        await context.ChatConversations
            .Where(c => c.Id == conversationId && c.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UpdatedAt, DateTime.UtcNow));
        await context.SaveChangesAsync();
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
