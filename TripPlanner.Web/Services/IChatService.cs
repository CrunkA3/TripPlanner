namespace TripPlanner.Web.Services;

public sealed record DisplayMessage(string Role, string Content);

public interface IChatService
{
    string? CurrentConversationId { get; }
    IReadOnlyList<DisplayMessage> Messages { get; }
    void SetUserLocation(double latitude, double longitude);
    void SetUserLanguage(string? language);
    void Clear();
    void SetCurrentConversationId(string conversationId);
    Task<bool> LoadConversationAsync(string conversationId, string userId);
    Task<string> SendMessageAsync(string userMessage, string userId, CancellationToken ct = default);
    Task<string> RunInferenceAsync(string userId, CancellationToken ct = default);
}
