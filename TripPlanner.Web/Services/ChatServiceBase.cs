using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TripPlanner.Web.Models;
using TripPlanner.Web.Repositories;

namespace TripPlanner.Web.Services;

/// <summary>
/// Abstract base class shared by <see cref="OllamaChatService"/> and <see cref="OpenAIChatService"/>.
/// Contains all conversation-management and tool-execution logic; subclasses only implement
/// the provider-specific HTTP inference call.
/// </summary>
public abstract partial class ChatServiceBase(
    IConfiguration configuration,
    ILogger logger,
    ITripRepository tripRepository,
    IWishlistRepository wishlistRepository,
    IPlaceRepository placeRepository,
    IChatConversationRepository conversationRepository,
    WeatherService weatherService) : IChatService
{
    // ── Inner message/tool-call types ────────────────────────────────────────────
    // These are deliberately kept internal so subclasses can share the same types.

    protected sealed class ChatMessage
    {
        [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
        [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;

        [JsonPropertyName("tool_calls"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ChatToolCall>? ToolCalls { get; set; }

        // Populated only for "tool" messages when used with OpenAI (tool_call_id must match
        // the id of the preceding tool_call in the assistant message).
        [JsonPropertyName("tool_call_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ToolCallId { get; set; }
    }

    protected sealed class ChatToolCall
    {
        // Optional: set by OpenAI, absent in Ollama format.
        [JsonPropertyName("id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Id { get; set; }

        [JsonPropertyName("function")] public ChatToolCallFunction Function { get; set; } = new();
    }

    protected sealed class ChatToolCallFunction
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        // Stored as a JSON object (Ollama format). OpenAI returns a JSON string; the
        // OpenAIChatService converts it to a JsonElement before storing here.
        [JsonPropertyName("arguments")] public JsonElement Arguments { get; set; }
    }

    protected static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // Expose injected dependencies as protected properties so derived classes can use them
    // without re-capturing the primary-constructor parameters (avoids CS9107 warnings).
    protected IConfiguration Configuration => configuration;
    protected IChatConversationRepository ConversationRepository => conversationRepository;

    // ── State ────────────────────────────────────────────────────────────────────

    private double? _userLatitude;
    private double? _userLongitude;

    /// <summary>Stores the user's current geographic position for inclusion in the system prompt.</summary>
    public void SetUserLocation(double latitude, double longitude)
    {
        _userLatitude = latitude;
        _userLongitude = longitude;
    }

    // Default maximum number of messages kept in the conversation history (≈ 20 turns).
    // Older messages are dropped to prevent unbounded payloads and memory growth.
    // Can be overridden via "Ollama:MaxHistoryMessages" in configuration.
    private const int DefaultMaxHistoryMessages = 40;

    protected readonly List<ChatMessage> History = [];

    /// <summary>The ID of the currently active persisted conversation, or null if no conversation is loaded.</summary>
    public string? CurrentConversationId { get; private set; }

    public IReadOnlyList<DisplayMessage> Messages =>
        History
            .Where(m => m.Role is "user" or "assistant" && !string.IsNullOrEmpty(m.Content))
            .Select(m => new DisplayMessage(m.Role, m.Content))
            .ToList();

    // ── Public API ───────────────────────────────────────────────────────────────

    /// <summary>Clears the in-memory history and detaches from any active conversation (does not delete from DB).</summary>
    public void Clear()
    {
        History.Clear();
        CurrentConversationId = null;
    }

    /// <summary>Sets the current conversation ID without loading history (used when the conversation was created externally).</summary>
    public void SetCurrentConversationId(string conversationId) => CurrentConversationId = conversationId;

    /// <summary>Loads a persisted conversation into the in-memory history so the user can continue it.</summary>
    /// <returns><c>true</c> if the conversation was found and loaded; <c>false</c> if it was not found.</returns>
    public async Task<bool> LoadConversationAsync(string conversationId, string userId)
    {
        var conversation = await conversationRepository.GetByIdAsync(conversationId, userId);
        if (conversation is null) return false;

        History.Clear();
        CurrentConversationId = conversationId;

        foreach (var msg in conversation.Messages.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id))
        {
            var chatMsg = new ChatMessage { Role = msg.Role, Content = msg.Content, ToolCallId = msg.ToolCallId };
            if (msg.ToolCallsJson is not null)
            {
                try
                {
                    chatMsg.ToolCalls = JsonSerializer.Deserialize<List<ChatToolCall>>(msg.ToolCallsJson, SerializerOptions);
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "Failed to deserialize ToolCallsJson for message in conversation {ConversationId}; tool calls will be omitted.", conversationId);
                }
            }
            History.Add(chatMsg);
        }

        TrimHistory();
        return true;
    }

    public async Task<string> SendMessageAsync(string userMessage, string userId, CancellationToken ct = default)
    {
        // Create a new persisted conversation on the first message
        if (CurrentConversationId is null)
        {
            var title = userMessage.Length > 80 ? userMessage[..77] + "…" : userMessage;
            var conversation = await conversationRepository.CreateAsync(userId, title);
            CurrentConversationId = conversation.Id;
        }

        History.Add(new ChatMessage { Role = "user", Content = userMessage });
        await conversationRepository.AddMessageAsync(CurrentConversationId, "user", userMessage, userId);
        TrimHistory();

        return await RunInferenceAsync(userId, ct);
    }

    /// <summary>
    /// Runs the provider-specific inference loop. Implemented by <see cref="OllamaChatService"/>
    /// and <see cref="OpenAIChatService"/>.
    /// </summary>
    public abstract Task<string> RunInferenceAsync(string userId, CancellationToken ct = default);

    // ── History helpers ──────────────────────────────────────────────────────────

    protected void TrimHistory()
    {
        var maxMessages = int.TryParse(configuration["Ollama:MaxHistoryMessages"], out var n) && n > 0
            ? n
            : DefaultMaxHistoryMessages;
        if (History.Count > maxMessages)
            History.RemoveRange(0, History.Count - maxMessages);
    }

    protected ChatMessage BuildSystemMessage()
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a helpful travel planning assistant for TripPlanner.");
        sb.AppendLine("You help users manage their trips, wishlists, and places.");
        sb.AppendLine("Use the available tools to access and modify the user's data.");
        sb.AppendLine($"Today's date is {DateTime.UtcNow:yyyy-MM-dd} (UTC).");
        if (_userLatitude.HasValue && _userLongitude.HasValue)
        {
            sb.AppendLine($"The user's current location is latitude {_userLatitude.Value.ToString("F4", CultureInfo.InvariantCulture)}, " +
                          $"longitude {_userLongitude.Value.ToString("F4", CultureInfo.InvariantCulture)}.");
            sb.AppendLine("Use the get_weather tool to look up current or forecasted weather for any location.");
        }
        sb.Append("Always be concise and helpful.");
        return new ChatMessage { Role = "system", Content = sb.ToString() };
    }

    // ── Tool execution ───────────────────────────────────────────────────────────

    protected async Task<string> ExecuteToolAsync(ChatToolCall toolCall, string userId, CancellationToken ct)
    {
        var name = toolCall.Function.Name;
        var args = toolCall.Function.Arguments;
        logger.LogDebug("Executing tool: {Tool}", name);

        try
        {
            return name switch
            {
                "list_trips" => await ListTripsAsync(userId),
                "get_trip" => await GetTripAsync(Str(args, "trip_id"), userId),
                "create_trip" => Str(args, "name") is { } tripName && !string.IsNullOrWhiteSpace(tripName)
                    ? await CreateTripAsync(tripName, Str(args, "description"), Str(args, "start_date"), Str(args, "end_date"), userId)
                    : "Missing required parameter: name",
                "update_trip" => Str(args, "trip_id") is { } updateTripId && !string.IsNullOrWhiteSpace(updateTripId)
                    ? await UpdateTripAsync(updateTripId, Str(args, "name"), Str(args, "description"), Str(args, "start_date"), Str(args, "end_date"), userId)
                    : "Missing required parameter: trip_id",
                "delete_trip" => Str(args, "trip_id") is { } deleteTripId && !string.IsNullOrWhiteSpace(deleteTripId)
                    ? await DeleteTripAsync(deleteTripId, userId)
                    : "Missing required parameter: trip_id",
                "list_wishlists" => await ListWishlistsAsync(userId),
                "get_wishlist" => Str(args, "wishlist_id") is { } wishlistId && !string.IsNullOrWhiteSpace(wishlistId)
                    ? await GetWishlistAsync(wishlistId, userId)
                    : "Missing required parameter: wishlist_id",
                "create_wishlist" => Str(args, "name") is { } wishlistName && !string.IsNullOrWhiteSpace(wishlistName)
                    ? await CreateWishlistAsync(wishlistName, Str(args, "description"), userId)
                    : "Missing required parameter: name",
                "update_wishlist" => Str(args, "wishlist_id") is { } updateWishlistId && !string.IsNullOrWhiteSpace(updateWishlistId)
                    ? await UpdateWishlistAsync(updateWishlistId, Str(args, "name"), Str(args, "description"), userId)
                    : "Missing required parameter: wishlist_id",
                "delete_wishlist" => Str(args, "wishlist_id") is { } deleteWishlistId && !string.IsNullOrWhiteSpace(deleteWishlistId)
                    ? await DeleteWishlistAsync(deleteWishlistId, userId)
                    : "Missing required parameter: wishlist_id",
                "list_places" => await ListPlacesAsync(Str(args, "category"), userId),
                "get_place" => Str(args, "place_id") is { } getPlaceId && !string.IsNullOrWhiteSpace(getPlaceId)
                    ? await GetPlaceAsync(getPlaceId, userId)
                    : "Missing required parameter: place_id",
                "create_place_wishlist" => await CreatePlaceWishlistAsync(args, userId),
                "create_place_trip" => await CreatePlaceTripAsync(args, userId),
                "update_place" => await UpdatePlaceAsync(args, userId),
                "delete_place" => Str(args, "place_id") is { } placeId && !string.IsNullOrWhiteSpace(placeId)
                    ? await DeletePlaceAsync(placeId, userId)
                    : "Missing required parameter: place_id",
                "get_weather" => TryGetDouble(args, "latitude", out var wLat) && TryGetDouble(args, "longitude", out var wLon)
                    ? await GetWeatherAsync(wLat, wLon, Str(args, "date"))
                    : "Missing required parameters: latitude, longitude",
                _ => $"Unknown tool: {name}"
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Tool {Tool} failed", name);
            return $"Tool execution failed: {ex.Message}";
        }
    }

    // ── JSON helpers ─────────────────────────────────────────────────────────────

    protected static string? Str(JsonElement args, string key)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(key, out var val))
            return null;
        return val.ValueKind switch
        {
            JsonValueKind.String => val.GetString(),
            JsonValueKind.Number => val.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    protected static bool TryGetDouble(JsonElement args, string key, out double value)
    {
        if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty(key, out var val))
        {
            if (val.ValueKind == JsonValueKind.Number) { value = val.GetDouble(); return true; }
            if (val.ValueKind == JsonValueKind.String && double.TryParse(val.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return true;
        }
        value = 0;
        return false;
    }

    // ── Trip tools ──────────────────────────────────────────────────────────────

    private async Task<string> ListTripsAsync(string userId)
    {
        var owned = await tripRepository.GetByOwnerAsync(userId);
        var shared = await tripRepository.GetSharedWithUserAsync(userId);
        var all = owned.Concat(shared).DistinctBy(t => t.Id);
        return JsonSerializer.Serialize(all.Select(t => new
        {
            t.Id,
            t.Name,
            t.Description,
            StartDate = t.StartDate?.ToString("yyyy-MM-dd"),
            EndDate = t.EndDate?.ToString("yyyy-MM-dd"),
            DayCount = t.Days.Count
        }));
    }

    private async Task<string> GetTripAsync(string? tripId, string userId)
    {
        if (tripId is null) return "Missing trip_id.";
        if (!await tripRepository.CanUserAccessAsync(tripId, userId)) return "Trip not found or access denied.";
        var trip = await tripRepository.GetByIdAsync(tripId);
        if (trip is null) return "Trip not found.";
        return JsonSerializer.Serialize(new
        {
            trip.Id,
            trip.Name,
            trip.Description,
            StartDate = trip.StartDate?.ToString("yyyy-MM-dd"),
            EndDate = trip.EndDate?.ToString("yyyy-MM-dd"),
            Days = trip.Days.OrderBy(d => d.DayNumber).Select(d => new
            {
                d.Id,
                d.DayNumber,
                Date = d.Date?.ToString("yyyy-MM-dd"),
                Places = d.Places.OrderBy(p => p.Order).Select(p => new
                {
                    TripPlaceId = p.Id,
                    p.PlaceId,
                    PlaceName = p.Place?.Name,
                    ScheduledTime = p.ScheduledTime?.ToString("HH:mm"),
                    p.DurationMinutes,
                    p.Notes,
                    p.Order
                })
            }),
            UnscheduledPlaces = trip.UnscheduledPlaces.Select(p => new
            {
                TripPlaceId = p.Id,
                p.PlaceId,
                PlaceName = p.Place?.Name
            }),
            Accommodations = trip.Accommodations.Select(a => new
            {
                a.Id,
                a.Name,
                a.Address,
                CheckIn = a.PlannedCheckIn?.ToString("yyyy-MM-dd"),
                CheckOut = a.PlannedCheckOut?.ToString("yyyy-MM-dd")
            })
        });
    }

    private async Task<string> CreateTripAsync(string name, string? description, string? startDate, string? endDate, string userId)
    {
        var trip = new Trip
        {
            Name = name,
            Description = description ?? string.Empty,
            OwnerId = userId,
            StartDate = startDate is not null && DateTime.TryParseExact(startDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sd) ? sd : null,
            EndDate = endDate is not null && DateTime.TryParseExact(endDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ed) ? ed : null
        };
        var created = await tripRepository.AddAsync(trip);
        return JsonSerializer.Serialize(new { created.Id, created.Name, Message = "Trip created successfully." });
    }

    private async Task<string> UpdateTripAsync(string tripId, string? name, string? description, string? startDate, string? endDate, string userId)
    {
        var trip = await tripRepository.GetByIdAsync(tripId);
        if (trip is null || trip.OwnerId != userId) return "Trip not found or access denied.";
        if (name is not null) trip.Name = name;
        if (description is not null) trip.Description = description;
        if (startDate is not null && DateTime.TryParseExact(startDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sd)) trip.StartDate = sd;
        if (endDate is not null && DateTime.TryParseExact(endDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ed)) trip.EndDate = ed;
        await tripRepository.UpdateAsync(trip);
        return "Trip updated successfully.";
    }

    private async Task<string> DeleteTripAsync(string tripId, string userId)
    {
        var trip = await tripRepository.GetByIdAsync(tripId);
        if (trip is null || trip.OwnerId != userId) return "Trip not found or access denied.";
        await tripRepository.DeleteAsync(tripId);
        return "Trip deleted successfully.";
    }

    // ── Wishlist tools ───────────────────────────────────────────────────────────

    private async Task<string> ListWishlistsAsync(string userId)
    {
        var wishlists = await wishlistRepository.GetAllByUserAsync(userId);
        return JsonSerializer.Serialize(wishlists.Select(w => new
        {
            w.Id,
            w.Name,
            w.Description,
            PlaceCount = w.Places.Count,
            CreatedAt = w.CreatedAt.ToString("yyyy-MM-dd")
        }));
    }

    private async Task<string> GetWishlistAsync(string wishlistId, string userId)
    {
        if (!await wishlistRepository.CanUserAccessAsync(wishlistId, userId)) return "Wishlist not found or access denied.";
        var wishlist = await wishlistRepository.GetByIdAsync(wishlistId);
        if (wishlist is null) return "Wishlist not found.";
        return JsonSerializer.Serialize(new
        {
            wishlist.Id,
            wishlist.Name,
            wishlist.Description,
            CreatedAt = wishlist.CreatedAt.ToString("yyyy-MM-dd"),
            Places = wishlist.Places.Select(p => new
            {
                p.Id,
                p.Name,
                p.Category,
                p.Latitude,
                p.Longitude,
                p.Description,
                VisitDate = p.VisitDate?.ToString("yyyy-MM-dd")
            })
        });
    }

    private async Task<string> CreateWishlistAsync(string name, string? description, string userId)
    {
        var wishlist = new Wishlist
        {
            Name = name,
            Description = description,
            UpdatedAt = DateTime.UtcNow
        };
        wishlist.SharedWith.Add(new UserWishlist
        {
            UserId = userId,
            WishlistId = wishlist.Id,
            Level = ShareLevel.Owner
        });
        var created = await wishlistRepository.AddAsync(wishlist);
        return JsonSerializer.Serialize(new { created.Id, created.Name, Message = "Wishlist created successfully." });
    }

    private async Task<string> UpdateWishlistAsync(string wishlistId, string? name, string? description, string userId)
    {
        if (!await wishlistRepository.CanUserEditAsync(wishlistId, userId)) return "Wishlist not found or access denied.";
        var wishlist = await wishlistRepository.GetByIdAsync(wishlistId);
        if (wishlist is null) return "Wishlist not found.";
        if (name is not null) wishlist.Name = name;
        if (description is not null) wishlist.Description = description;
        await wishlistRepository.UpdateAsync(wishlist);
        return "Wishlist updated successfully.";
    }

    private async Task<string> DeleteWishlistAsync(string wishlistId, string userId)
    {
        if (!await wishlistRepository.CanUserAdministrateAsync(wishlistId, userId)) return "Wishlist not found or access denied.";
        await wishlistRepository.DeleteAsync(wishlistId);
        return "Wishlist deleted successfully.";
    }

    // ── Place tools ──────────────────────────────────────────────────────────────

    private async Task<string> ListPlacesAsync(string? category, string userId)
    {
        var places = await placeRepository.GetAllByUserAsync(userId);
        if (category is not null && Enum.TryParse<PlaceCategory>(category, true, out var cat))
            places = places.Where(p => p.Category == cat).ToList();
        return JsonSerializer.Serialize(places.Select(p => new
        {
            p.Id,
            p.Name,
            p.Category,
            p.Latitude,
            p.Longitude,
            p.Description,
            WishlistId = p.WishlistId,
            WishlistName = p.Wishlist?.Name,
            VisitDate = p.VisitDate?.ToString("yyyy-MM-dd"),
            Tags = p.Tags
        }));
    }

    private async Task<string> GetPlaceAsync(string placeId, string userId)
    {
        var place = await placeRepository.GetByIdAsync(placeId, userId);
        if (place is null) return "Place not found.";
        return JsonSerializer.Serialize(new
        {
            place.Id,
            place.Name,
            place.Category,
            place.Latitude,
            place.Longitude,
            place.Description,
            place.Notes,
            place.Url,
            WishlistId = place.WishlistId,
            WishlistName = place.Wishlist?.Name,
            VisitDate = place.VisitDate?.ToString("yyyy-MM-dd"),
            Tags = place.Tags
        });
    }

    private async Task<string> CreatePlaceWishlistAsync(JsonElement args, string userId)
    {
        var name = Str(args, "name");
        var category = Str(args, "category");
        var wishlistId = Str(args, "wishlist_id");
        if (name is null || category is null || wishlistId is null)
            return "Missing required parameters: name, category, wishlist_id.";
        if (!Enum.TryParse<PlaceCategory>(category, true, out var cat))
            return $"Invalid category: {category}. Valid values: {string.Join(", ", Enum.GetNames<PlaceCategory>())}";
        if (!TryGetDouble(args, "latitude", out var lat) || !TryGetDouble(args, "longitude", out var lon))
            return "Missing required parameters: latitude, longitude.";

        if (!await wishlistRepository.CanUserEditAsync(wishlistId, userId))
            return "Access denied: you do not have permission to add places to this wishlist.";

        var place = new Models.Place
        {
            Name = name,
            Category = cat,
            Latitude = lat,
            Longitude = lon,
            WishlistId = wishlistId,
            Description = Str(args, "description") ?? string.Empty,
            Notes = Str(args, "notes"),
            Url = Str(args, "url"),
            VisitDate = Str(args, "visit_date") is { } vd && DateTime.TryParseExact(vd, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var vdDt) ? vdDt : null,
            Tags = Str(args, "tags")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() ?? []
        };
        var created = await placeRepository.AddAsync(place);
        return JsonSerializer.Serialize(new { created.Id, created.Name, Message = "Place created successfully." });
    }

    private async Task<string> CreatePlaceTripAsync(JsonElement args, string userId)
    {
        var name = Str(args, "name");
        var category = Str(args, "category");
        var tripId = Str(args, "trip_id");
        if (name is null || category is null || tripId is null)
            return "Missing required parameters: name, category, trip_id.";
        if (!Enum.TryParse<PlaceCategory>(category, true, out var cat))
            return $"Invalid category: {category}. Valid values: {string.Join(", ", Enum.GetNames<PlaceCategory>())}";
        if (!TryGetDouble(args, "latitude", out var lat) || !TryGetDouble(args, "longitude", out var lon))
            return "Missing required parameters: latitude, longitude.";

        if (!await tripRepository.CanUserAccessAsync(tripId, userId))
            return "Access denied: you do not have permission to add places to this trip.";

        var place = new Models.Place
        {
            Name = name,
            Category = cat,
            Latitude = lat,
            Longitude = lon,
            TripId = tripId,
            Description = Str(args, "description") ?? string.Empty,
            Notes = Str(args, "notes"),
            Url = Str(args, "url"),
            VisitDate = Str(args, "visit_date") is { } vd && DateTime.TryParseExact(vd, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var vdDt) ? vdDt : null,
            Tags = Str(args, "tags")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() ?? []
        };
        var created = await placeRepository.AddAsync(place);
        return JsonSerializer.Serialize(new { created.Id, created.Name, Message = "Place created successfully." });
    }

    private async Task<string> UpdatePlaceAsync(JsonElement args, string userId)
    {
        var placeId = Str(args, "place_id");
        if (placeId is null) return "Missing place_id.";
        var place = await placeRepository.GetByIdAsync(placeId, userId);
        if (place is null) return "Place not found.";
        if (Str(args, "name") is { } n) place.Name = n;
        if (Str(args, "description") is { } d) place.Description = d;
        if (Str(args, "notes") is { } no) place.Notes = no;
        if (Str(args, "url") is { } u) place.Url = u;
        if (Str(args, "visit_date") is { } vd && DateTime.TryParseExact(vd, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var vdDt)) place.VisitDate = vdDt;
        if (Str(args, "tags") is { } t)
            place.Tags = t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        place.UpdatedAt = DateTime.UtcNow;
        await placeRepository.UpdateAsync(place);
        return "Place updated successfully.";
    }

    private async Task<string> DeletePlaceAsync(string placeId, string userId)
    {
        var place = await placeRepository.GetByIdAsync(placeId, userId);
        if (place is null) return "Place not found.";
        await placeRepository.DeleteAsync(placeId, userId);
        return "Place deleted successfully.";
    }

    // ── Weather tool ─────────────────────────────────────────────────────────────

    private async Task<string> GetWeatherAsync(double latitude, double longitude, string? dateStr)
    {
        if (dateStr is not null)
        {
            if (!DateOnly.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return $"Invalid date format '{dateStr}'. Please provide the date in yyyy-MM-dd format.";

            var day = await weatherService.GetWeatherForDateAsync(latitude, longitude, date);
            if (day is null)
                return $"No weather data available for {dateStr} at ({latitude.ToString("F4", CultureInfo.InvariantCulture)}, {longitude.ToString("F4", CultureInfo.InvariantCulture)}).";
            return $"Weather on {dateStr} at ({latitude.ToString("F4", CultureInfo.InvariantCulture)}, {longitude.ToString("F4", CultureInfo.InvariantCulture)}): " +
                   $"{day.GetDescription()} {day.GetIcon()}, " +
                   $"{day.TempMin?.ToString("F0", CultureInfo.InvariantCulture) ?? "?"}°–{day.TempMax?.ToString("F0", CultureInfo.InvariantCulture) ?? "?"}°C, " +
                   $"precipitation: {day.Precipitation?.ToString("F1", CultureInfo.InvariantCulture) ?? "0"}mm.";
        }

        // No date given – return the 7-day forecast.
        var forecast = await weatherService.GetForecastAsync(latitude, longitude);
        if (forecast is null || forecast.Daily.Count == 0)
            return $"No weather data available for ({latitude.ToString("F4", CultureInfo.InvariantCulture)}, {longitude.ToString("F4", CultureInfo.InvariantCulture)}).";

        var sb = new StringBuilder();
        sb.AppendLine($"7-day weather forecast for ({latitude.ToString("F4", CultureInfo.InvariantCulture)}, {longitude.ToString("F4", CultureInfo.InvariantCulture)}):");
        foreach (var d in forecast.Daily.Take(7))
            sb.AppendLine($"  {d.Date:yyyy-MM-dd}: {d.GetDescription()} {d.GetIcon()}, " +
                          $"{d.TempMin?.ToString("F0", CultureInfo.InvariantCulture) ?? "?"}°–{d.TempMax?.ToString("F0", CultureInfo.InvariantCulture) ?? "?"}°C, " +
                          $"precipitation: {d.Precipitation?.ToString("F1", CultureInfo.InvariantCulture) ?? "0"}mm");
        return sb.ToString();
    }

    // ── Tool definitions ─────────────────────────────────────────────────────────

    protected static readonly object[] ToolDefinitions = BuildToolDefinitions();

    private static object[] BuildToolDefinitions() =>
    [
        MakeTool("list_trips", "List all trips owned by or shared with the current user."),
        MakeTool("get_trip", "Get the full details of a trip including all days and scheduled places.",
            Props(("trip_id", "string", "The trip ID.")),
            ["trip_id"]),
        MakeTool("create_trip", "Create a new trip.",
            Props(
                ("name", "string", "The name of the trip."),
                ("description", "string", "An optional description."),
                ("start_date", "string", "Optional start date in ISO 8601 format (yyyy-MM-dd)."),
                ("end_date", "string", "Optional end date in ISO 8601 format (yyyy-MM-dd).")),
            ["name"]),
        MakeTool("update_trip", "Update an existing trip's metadata (name, description, dates).",
            Props(
                ("trip_id", "string", "The ID of the trip to update."),
                ("name", "string", "New name (optional)."),
                ("description", "string", "New description (optional)."),
                ("start_date", "string", "New start date yyyy-MM-dd (optional)."),
                ("end_date", "string", "New end date yyyy-MM-dd (optional).")),
            ["trip_id"]),
        MakeTool("delete_trip", "Delete a trip and all its contents.",
            Props(("trip_id", "string", "The ID of the trip to delete.")),
            ["trip_id"]),

        MakeTool("list_wishlists", "List all wishlists accessible to the current user."),
        MakeTool("get_wishlist", "Get the details of a wishlist including its places.",
            Props(("wishlist_id", "string", "The wishlist ID.")),
            ["wishlist_id"]),
        MakeTool("create_wishlist", "Create a new wishlist.",
            Props(
                ("name", "string", "The name of the wishlist."),
                ("description", "string", "An optional description.")),
            ["name"]),
        MakeTool("update_wishlist", "Update a wishlist's name or description.",
            Props(
                ("wishlist_id", "string", "The ID of the wishlist to update."),
                ("name", "string", "New name (optional)."),
                ("description", "string", "New description (optional).")),
            ["wishlist_id"]),
        MakeTool("delete_wishlist", "Delete a wishlist.",
            Props(("wishlist_id", "string", "The ID of the wishlist to delete.")),
            ["wishlist_id"]),

        MakeTool("list_places", "List all places accessible to the current user, optionally filtered by category.",
            Props(("category", "string",
                "Optional category filter: Viewpoint, Museum, Restaurant, Nature, Activity, Accommodation, Shopping, Entertainment, Race, Other."))),
        MakeTool("get_place", "Get details of a specific place from a Wishlist or Trip by ID.",
            Props(("place_id", "string", "The place ID.")),
            ["place_id"]),
        MakeTool("create_place_wishlist", "Create a new place in a wishlist.",
            Props(
                ("name", "string", "The name of the place."),
                ("category", "string",
                    "Category: Viewpoint, Museum, Restaurant, Nature, Activity, Accommodation, Shopping, Entertainment, Race, Other."),
                ("latitude", "number", "Latitude coordinate."),
                ("longitude", "number", "Longitude coordinate."),
                ("wishlist_id", "string", "The wishlist ID to add this place to."),
                ("description", "string", "Optional description."),
                ("notes", "string", "Optional notes."),
                ("url", "string", "Optional URL with more information."),
                ("visit_date", "string", "Optional visit date in yyyy-MM-dd format."),
                ("tags", "string", "Optional comma-separated tags.")),
            ["name", "category", "latitude", "longitude", "wishlist_id"]),
        MakeTool("create_place_trip", "Create a new place in a trip.",
            Props(
                ("name", "string", "The name of the place."),
                ("category", "string",
                    "Category: Viewpoint, Museum, Restaurant, Nature, Activity, Accommodation, Shopping, Entertainment, Race, Other."),
                ("latitude", "number", "Latitude coordinate."),
                ("longitude", "number", "Longitude coordinate."),
                ("trip_id", "string", "The trip ID to add this place to."),
                ("description", "string", "Optional description."),
                ("notes", "string", "Optional notes."),
                ("url", "string", "Optional URL with more information."),
                ("visit_date", "string", "Optional visit date in yyyy-MM-dd format."),
                ("tags", "string", "Optional comma-separated tags.")),
            ["name", "category", "latitude", "longitude", "trip_id"]),
        MakeTool("update_place", "Update an existing place's details.",
            Props(
                ("place_id", "string", "The ID of the place to update."),
                ("name", "string", "New name (optional)."),
                ("description", "string", "New description (optional)."),
                ("notes", "string", "New notes (optional)."),
                ("url", "string", "New URL (optional)."),
                ("visit_date", "string", "New visit date yyyy-MM-dd (optional)."),
                ("tags", "string", "New comma-separated tags (optional).")),
            ["place_id"]),
        MakeTool("delete_place", "Delete a place.",
            Props(("place_id", "string", "The ID of the place to delete.")),
            ["place_id"]),

        MakeTool("get_weather", "Get the weather forecast for a given location. If a date is provided returns the weather for that day; otherwise returns a 7-day forecast.",
            Props(
                ("latitude", "number", "Latitude of the location."),
                ("longitude", "number", "Longitude of the location."),
                ("date", "string", "Optional date in yyyy-MM-dd format. If omitted, the full 7-day forecast is returned.")),
            ["latitude", "longitude"]),
    ];

    private static Dictionary<string, object> Props(params (string Name, string Type, string Desc)[] props) =>
        props.ToDictionary(p => p.Name, p => (object)new { type = p.Type, description = p.Desc });

    private static object MakeTool(string name, string description,
        Dictionary<string, object>? properties = null, string[]? required = null) =>
        new
        {
            type = "function",
            function = new
            {
                name,
                description,
                parameters = new
                {
                    type = "object",
                    properties = properties ?? [],
                    required = required ?? []
                }
            }
        };
}
