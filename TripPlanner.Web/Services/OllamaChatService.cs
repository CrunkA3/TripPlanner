using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TripPlanner.Web.Repositories;

namespace TripPlanner.Web.Services;

// OllamaChatService is registered as Scoped: in Blazor Server each browser tab/window
// creates its own SignalR circuit and therefore its own service instance, so conversation
// history is naturally isolated per tab.
public partial class OllamaChatService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<OllamaChatService> logger,
    ITripRepository tripRepository,
    IWishlistRepository wishlistRepository,
    IPlaceRepository placeRepository,
    IChatConversationRepository conversationRepository,
    WeatherService weatherService,
    TransitService transitService,
    BrowserTimeZoneService browserTimeZoneService)
    : ChatServiceBase(configuration, logger, tripRepository, wishlistRepository, placeRepository, conversationRepository, weatherService, transitService, browserTimeZoneService)
{
    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")] public ChatMessage? Message { get; set; }
        [JsonPropertyName("done")] public bool Done { get; set; }
    }

    /// <summary>
    /// Runs the Ollama inference loop on the already-loaded conversation history and saves
    /// the assistant response to the database. Call <see cref="ChatServiceBase.LoadConversationAsync"/> (or
    /// <see cref="ChatServiceBase.SendMessageAsync"/> which adds the user message) before invoking this method.
    /// </summary>
    public override async Task<string> RunInferenceAsync(string userId, CancellationToken ct = default)
    {
        if (CurrentConversationId is null)
            throw new InvalidOperationException("No active conversation. Call LoadConversationAsync or SendMessageAsync first.");

        var model = Configuration["Ollama:Model"] ?? "llama3.2";
        var client = httpClientFactory.CreateClient("Ollama");
        var systemMessage = BuildSystemMessage();

        const int maxIterations = 10;
        for (var i = 0; i < maxIterations; i++)
        {
            var messages = new List<ChatMessage> { systemMessage };
            messages.AddRange(History);

            var requestObj = new
            {
                model,
                messages,
                tools = ToolDefinitions,
                stream = true
            };

            List<string> responseLines = [];
            try
            {
                var requestJson = JsonSerializer.Serialize(requestObj, SerializerOptions);
                using var httpContent = new StringContent(requestJson, Encoding.UTF8, "application/json");
                using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat") { Content = httpContent };
                var httpResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                httpResponse.EnsureSuccessStatusCode();

                using var stream = await httpResponse.Content.ReadAsStreamAsync(ct);
                using var reader = new StreamReader(stream);

                string? line;
                while ((line = await reader.ReadLineAsync(ct)) is not null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    responseLines.Add(line);
                    LogData(line);
                }

                /// example response
                /// {"model":"llama3.2","created_at":"2026-03-10T17:00:54.822089596Z","message":{"role":"assistant","content":"","tool_calls":[{"id":"call_ejnj3d2t","function":{"index":0,"name":"get_place","arguments":{"place_id":"48.8928, 11.4133"}}}]},"done":false}
                /// {"model":"llama3.2","created_at":"2026-03-10T17:00:54.899038957Z","message":{"role":"assistant","content":""},"done":true,"done_reason":"stop","total_duration":2194658438,"load_duration":125007901,"prompt_eval_count":1535,"prompt_eval_duration":81825619,"eval_count":25,"eval_duration":1954616875}

            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to call Ollama /api/chat");
                var errMsg = $"I'm sorry, I couldn't connect to the AI service: {ex.Message}";
                History.Add(new ChatMessage { Role = "assistant", Content = errMsg });
                await ConversationRepository.AddMessageAsync(CurrentConversationId, "assistant", errMsg, userId);
                TrimHistory();
                return errMsg;
            }


            // The response contains the assistant's message and an optional list of tool calls to execute.
            foreach (var responseLine in responseLines)
            {
                var response = JsonSerializer.Deserialize<OllamaChatResponse>(responseLine, SerializerOptions);

                // if (response?.Done ?? false) break;

                if (response?.Message is null)
                {
                    const string noResponseMsg = "I'm sorry, I didn't receive a valid response.";
                    History.Add(new ChatMessage { Role = "assistant", Content = noResponseMsg });
                    await ConversationRepository.AddMessageAsync(CurrentConversationId, "assistant", noResponseMsg, userId);
                    TrimHistory();
                    return noResponseMsg;
                }

                History.Add(response.Message);

                if (response.Message.ToolCalls is null || response.Message.ToolCalls.Count == 0)
                {
                    var finalContent = response.Message.Content ?? string.Empty;
                    await ConversationRepository.AddMessageAsync(CurrentConversationId, "assistant", finalContent, userId);
                    TrimHistory();
                    return finalContent;
                }

                // Persist the assistant message that contains tool calls so that
                // reloaded conversations have the full context for follow-up turns.
                var toolCallsJson = JsonSerializer.Serialize(response.Message.ToolCalls, SerializerOptions);
                await ConversationRepository.AddMessageAsync(CurrentConversationId, "assistant",
                    response.Message.Content ?? string.Empty, userId, toolCallsJson);

                foreach (var toolCall in response.Message.ToolCalls)
                {
                    var toolResult = await ExecuteToolAsync(toolCall, userId, ct);
                    logger.LogInformation("Tool {Tool} executed. Result length: {Length}", toolCall.Function.Name,
                        toolResult.Length);
                    History.Add(new ChatMessage { Role = "tool", Content = toolResult });
                    await ConversationRepository.AddMessageAsync(CurrentConversationId, "tool", toolResult, userId);
                }
            }

            // Trim after each tool-call round so the next iteration's request payload
            // is bounded even when the loop continues.
            TrimHistory();
        }

        const string maxIterMsg = "I apologize, I reached the maximum number of steps. Please try a simpler question.";
        History.Add(new ChatMessage { Role = "assistant", Content = maxIterMsg });
        await ConversationRepository.AddMessageAsync(CurrentConversationId, "assistant", maxIterMsg, userId);
        TrimHistory();
        return maxIterMsg;
    }


    [LoggerMessage(Level = LogLevel.Debug, Message = "Data: {data}")]
    private partial void LogData(string data);
}
