using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TripPlanner.Web.Repositories;

namespace TripPlanner.Web.Services;

/// <summary>
/// Chat service implementation backed by the OpenAI Chat Completions API.
/// Registered when <c>AI:Provider</c> is set to <c>OpenAI</c> in configuration.
/// </summary>
public partial class OpenAIChatService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<OpenAIChatService> logger,
    ITripRepository tripRepository,
    IWishlistRepository wishlistRepository,
    IPlaceRepository placeRepository,
    IChatConversationRepository conversationRepository,
    WeatherService weatherService)
    : ChatServiceBase(configuration, logger, tripRepository, wishlistRepository, placeRepository, conversationRepository, weatherService)
{
    // ── OpenAI request message types ─────────────────────────────────────────────

    private sealed class OpenAIRequestMessage
    {
        /// <summary>
        /// Gets or sets the role associated with the user or entity.
        /// </summary>
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the textual content associated with this instance.
        /// </summary>
        [JsonPropertyName("content")]
        public string? Content { get; set; }

        /// <summary>
        /// Gets or sets the collection of tool calls associated with the request.
        /// </summary>
        /// <remarks>Each tool call in the collection represents an invocation of a tool as part of the
        /// request. The property may be null if no tool calls are present.</remarks>
        [JsonPropertyName("tool_calls"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<OpenAIRequestToolCall>? ToolCalls { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the tool call associated with this object.
        /// </summary>
        [JsonPropertyName("tool_call_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ToolCallId { get; set; }
    }

    private sealed class OpenAIRequestToolCall
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("type")] public string Type { get; set; } = "function";
        [JsonPropertyName("function")] public OpenAIRequestToolCallFunction Function { get; set; } = new();
    }

    private sealed class OpenAIRequestToolCallFunction
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        // OpenAI requires arguments as a JSON string, not an object.
        [JsonPropertyName("arguments")] public string Arguments { get; set; } = string.Empty;
    }

    // ── OpenAI SSE streaming response types ──────────────────────────────────────

    private sealed class OpenAIStreamChunk
    {
        [JsonPropertyName("choices")] public List<OpenAIStreamChoice> Choices { get; set; } = [];
    }

    private sealed class OpenAIStreamChoice
    {
        [JsonPropertyName("delta")] public OpenAIStreamDelta Delta { get; set; } = new();
        [JsonPropertyName("finish_reason")] public string? FinishReason { get; set; }
    }

    private sealed class OpenAIStreamDelta
    {
        [JsonPropertyName("role")] public string? Role { get; set; }
        [JsonPropertyName("content")] public string? Content { get; set; }
        [JsonPropertyName("tool_calls")] public List<OpenAIStreamToolCallDelta>? ToolCalls { get; set; }
    }

    private sealed class OpenAIStreamToolCallDelta
    {
        [JsonPropertyName("index")] public int Index { get; set; }
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("function")] public OpenAIStreamToolCallFunctionDelta? Function { get; set; }
    }

    private sealed class OpenAIStreamToolCallFunctionDelta
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("arguments")] public string? Arguments { get; set; }
    }

    // ── Inference ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the OpenAI inference loop using the Chat Completions API with SSE streaming.
    /// Tool calls are executed and the loop continues until the model produces a final answer
    /// or the maximum number of iterations is reached.
    /// </summary>
    public override async Task<string> RunInferenceAsync(string userId, CancellationToken ct = default)
    {
        if (CurrentConversationId is null)
            throw new InvalidOperationException("No active conversation. Call LoadConversationAsync or SendMessageAsync first.");

        var model = Configuration["OpenAI:Model"] ?? "gpt-4o";
        var client = httpClientFactory.CreateClient("OpenAI");
        var systemMessage = BuildSystemMessage();

        const int maxIterations = 10;
        for (var i = 0; i < maxIterations; i++)
        {
            // Convert internal ChatMessage history to the OpenAI request format.
            var messages = BuildOpenAIMessages(systemMessage, History);

            var requestObj = new
            {
                model,
                messages,
                tools = ToolDefinitions,
                stream = true
            };

            var contentAccumulated = new StringBuilder();
            // key = tool-call index, value = (id, name, arguments-builder)
            var toolCallAccum = new Dictionary<int, (string Id, string Name, StringBuilder Arguments)>();

            try
            {
                var requestJson = JsonSerializer.Serialize(requestObj, SerializerOptions);
                using var httpContent = new StringContent(requestJson, Encoding.UTF8, "application/json");
                using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions") { Content = httpContent };
                var httpResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                httpResponse.EnsureSuccessStatusCode();

                using var stream = await httpResponse.Content.ReadAsStreamAsync(ct);
                using var reader = new StreamReader(stream);

                string? line;
                while ((line = await reader.ReadLineAsync(ct)) is not null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (!line.StartsWith("data: ", StringComparison.Ordinal)) continue;

                    var data = line["data: ".Length..];
                    if (data == "[DONE]") break;

                    LogChunk(data);

                    var chunk = JsonSerializer.Deserialize<OpenAIStreamChunk>(data, SerializerOptions);
                    var choice = chunk?.Choices.Count > 0 ? chunk.Choices[0] : null;
                    if (choice is null) continue;

                    if (choice.Delta.Content is not null)
                        contentAccumulated.Append(choice.Delta.Content);

                    if (choice.Delta.ToolCalls is not null)
                    {
                        foreach (var tc in choice.Delta.ToolCalls)
                        {
                            if (!toolCallAccum.TryGetValue(tc.Index, out var existing))
                            {
                                existing = (tc.Id ?? string.Empty, tc.Function?.Name ?? string.Empty, new StringBuilder());
                                toolCallAccum[tc.Index] = existing;
                            }
                            else
                            {
                                // Update id/name on first chunk that carries them.
                                var newId = !string.IsNullOrEmpty(tc.Id) ? tc.Id : existing.Id;
                                var newName = !string.IsNullOrEmpty(tc.Function?.Name) ? tc.Function.Name : existing.Name;
                                existing = (newId, newName, existing.Arguments);
                                toolCallAccum[tc.Index] = existing;
                            }

                            if (tc.Function?.Arguments is not null)
                                existing.Arguments.Append(tc.Function.Arguments);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                LogOpenAICallFailed(ex);
                var errMsg = $"I'm sorry, I couldn't connect to the AI service: {ex.Message}";
                History.Add(new ChatMessage { Role = "assistant", Content = errMsg });
                await ConversationRepository.AddMessageAsync(CurrentConversationId, "assistant", errMsg, userId);
                TrimHistory();
                return errMsg;
            }

            // Build the assistant ChatMessage from accumulated SSE deltas.
            List<ChatToolCall>? toolCalls = null;
            if (toolCallAccum.Count > 0)
            {
                toolCalls = [];
                foreach (var (_, (id, name, argsBuilder)) in toolCallAccum.OrderBy(x => x.Key))
                {
                    var argsJson = argsBuilder.ToString();
                    JsonElement argsElement;
                    try
                    {
                        argsElement = JsonDocument.Parse(argsJson.Length > 0 ? argsJson : "{}").RootElement.Clone();
                    }
                    catch
                    {
                        argsElement = JsonDocument.Parse("{}").RootElement.Clone();
                    }

                    toolCalls.Add(new ChatToolCall
                    {
                        // Use a deterministic index-based fallback if the API did not provide an ID
                        // (should not normally happen with OpenAI, but avoids random IDs that
                        // would fail to match tool-result messages on conversation reload).
                        Id = string.IsNullOrEmpty(id) ? $"tc_{toolCalls.Count}" : id,
                        Function = new ChatToolCallFunction { Name = name, Arguments = argsElement }
                    });
                }
            }

            var content = contentAccumulated.ToString();
            var assistantMessage = new ChatMessage { Role = "assistant", Content = content, ToolCalls = toolCalls };
            History.Add(assistantMessage);

            if (toolCalls is null || toolCalls.Count == 0)
            {
                await ConversationRepository.AddMessageAsync(CurrentConversationId, "assistant", content, userId);
                TrimHistory();
                return content;
            }

            // Persist the assistant message with tool calls.
            var toolCallsJson = JsonSerializer.Serialize(toolCalls, SerializerOptions);
            await ConversationRepository.AddMessageAsync(CurrentConversationId, "assistant", content, userId, toolCallsJson);

            // Execute each tool and record the result.
            foreach (var toolCall in toolCalls)
            {
                var toolResult = await ExecuteToolAsync(toolCall, userId, ct);
                LogToolResult(toolCall.Function.Name, toolResult[..Math.Min(200, toolResult.Length)]);

                History.Add(new ChatMessage { Role = "tool", Content = toolResult, ToolCallId = toolCall.Id });
                // Persist tool_call_id so reloaded conversations can reconstruct the correct
                // assistant→tool mapping without relying on positional heuristics.
                await ConversationRepository.AddMessageAsync(CurrentConversationId, "tool", toolResult, userId, toolCallId: toolCall.Id);
            }

            TrimHistory();
        }

        const string maxIterMsg = "I apologize, I reached the maximum number of steps. Please try a simpler question.";
        History.Add(new ChatMessage { Role = "assistant", Content = maxIterMsg });
        await ConversationRepository.AddMessageAsync(CurrentConversationId, "assistant", maxIterMsg, userId);
        TrimHistory();
        return maxIterMsg;
    }

    // ── Message format conversion ────────────────────────────────────────────────

    /// <summary>
    /// Converts the internal <see cref="ChatMessage"/> history (stored in Ollama-compatible format)
    /// into the list of request messages expected by the OpenAI Chat Completions API.
    /// The main differences handled here are:
    /// <list type="bullet">
    ///   <item>Tool-call <c>arguments</c> must be a JSON string, not an object.</item>
    ///   <item>Each tool-call needs a <c>type: "function"</c> field and a non-empty <c>id</c>.</item>
    ///   <item>Tool-result messages require a matching <c>tool_call_id</c>.</item>
    /// </list>
    /// </summary>
    private static List<OpenAIRequestMessage> BuildOpenAIMessages(ChatMessage systemMessage, IReadOnlyList<ChatMessage> history)
    {
        List<OpenAIRequestMessage> result =
        [
            ConvertToRequestMessage(systemMessage, toolCallIdOverride: null)
        ];

        // Track pending tool-call IDs so we can assign them to the following tool-result
        // messages even when the IDs were not stored (e.g. conversations created via Ollama).
        var pendingToolCallIds = new Queue<string>();

        foreach (var msg in history)
        {
            if (msg.Role == "assistant" && msg.ToolCalls?.Count > 0)
            {
                pendingToolCallIds.Clear();
                var requestToolCalls = new List<OpenAIRequestToolCall>();
                for (var idx = 0; idx < msg.ToolCalls.Count; idx++)
                {
                    var tc = msg.ToolCalls[idx];
                    // Use a distinct prefix for synthetic IDs so they are easily distinguishable
                    // from real OpenAI-generated IDs when debugging.
                    var id = !string.IsNullOrEmpty(tc.Id) ? tc.Id : $"synth_{idx}";
                    pendingToolCallIds.Enqueue(id);
                    requestToolCalls.Add(new OpenAIRequestToolCall
                    {
                        Id = id,
                        Type = "function",
                        Function = new OpenAIRequestToolCallFunction
                        {
                            Name = tc.Function.Name,
                            // Arguments must be a JSON string for OpenAI.
                            Arguments = tc.Function.Arguments.ValueKind == JsonValueKind.Undefined
                                ? "{}"
                                : tc.Function.Arguments.GetRawText()
                        }
                    });
                }

                result.Add(new OpenAIRequestMessage
                {
                    Role = "assistant",
                    Content = string.IsNullOrEmpty(msg.Content) ? null : msg.Content,
                    ToolCalls = requestToolCalls
                });
            }
            else if (msg.Role == "tool")
            {
                // Prefer the stored ToolCallId; fall back to the next pending ID (for Ollama history).
                var toolCallId = !string.IsNullOrEmpty(msg.ToolCallId)
                    ? msg.ToolCallId
                    : (pendingToolCallIds.Count > 0 ? pendingToolCallIds.Dequeue() : "unknown");

                result.Add(new OpenAIRequestMessage
                {
                    Role = "tool",
                    Content = msg.Content,
                    ToolCallId = toolCallId
                });
            }
            else
            {
                result.Add(ConvertToRequestMessage(msg, toolCallIdOverride: null));
            }
        }

        return result;
    }

    private static OpenAIRequestMessage ConvertToRequestMessage(ChatMessage msg, string? toolCallIdOverride) =>
        new()
        {
            Role = msg.Role,
            Content = msg.Content,
            ToolCallId = toolCallIdOverride ?? msg.ToolCallId
        };




    [LoggerMessage(Level = LogLevel.Debug, Message = "OpenAI chunk: {data}")]
    private partial void LogChunk(string data);


    [LoggerMessage(level: LogLevel.Warning, Message = "Failed to call OpenAI /v1/chat/completions")]
    private partial void LogOpenAICallFailed(Exception ex);


    [LoggerMessage(level: LogLevel.Information, Message = "Tool {tool} returned: {result}")]
    private partial void LogToolResult(string tool, string result);
}
