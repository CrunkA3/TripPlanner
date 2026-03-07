using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TripPlanner.Web.Models;
using TripPlanner.Web.Repositories;

namespace TripPlanner.Web.Services;

// OllamaChatService is registered as Scoped: in Blazor Server each browser tab/window
// creates its own SignalR circuit and therefore its own service instance, so conversation
// history is naturally isolated per tab.
public class OllamaChatService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<OllamaChatService> logger,
    ITripRepository tripRepository,
    IWishlistRepository wishlistRepository,
    IPlaceRepository placeRepository)
{
    public sealed record DisplayMessage(string Role, string Content);

    private sealed class OllamaMessage
    {
        [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
        [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;

        [JsonPropertyName("tool_calls"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<OllamaToolCall>? ToolCalls { get; set; }
    }

    private sealed class OllamaToolCall
    {
        [JsonPropertyName("function")] public OllamaToolCallFunction Function { get; set; } = new();
    }

    private sealed class OllamaToolCallFunction
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("arguments")] public JsonElement Arguments { get; set; }
    }

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")] public OllamaMessage? Message { get; set; }
        [JsonPropertyName("done")] public bool Done { get; set; }
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly OllamaMessage SystemMessage = new()
    {
        Role = "system",
        Content = "You are a helpful travel planning assistant for TripPlanner. " +
                  "You help users manage their trips, wishlists, and places. " +
                  "Use the available tools to access and modify the user's data. " +
                  "Always be concise and helpful."
    };

    private readonly List<OllamaMessage> _history = [];

    public IReadOnlyList<DisplayMessage> Messages =>
        _history
            .Where(m => m.Role is "user" or "assistant" && !string.IsNullOrEmpty(m.Content))
            .Select(m => new DisplayMessage(m.Role, m.Content))
            .ToList();

    public void Clear() => _history.Clear();

    public async Task<string> SendMessageAsync(string userMessage, string userId, CancellationToken ct = default)
    {
        _history.Add(new OllamaMessage { Role = "user", Content = userMessage });

        var model = configuration["Ollama:Model"] ?? "llama3.2";
        var client = httpClientFactory.CreateClient("Ollama");

        const int maxIterations = 10;
        for (var i = 0; i < maxIterations; i++)
        {
            var messages = new List<OllamaMessage> { SystemMessage };
            messages.AddRange(_history);

            var requestObj = new
            {
                model,
                messages,
                tools = ToolDefinitions,
                stream = false
            };

            string responseJson;
            try
            {
                var requestJson = JsonSerializer.Serialize(requestObj, SerializerOptions);
                using var httpContent = new StringContent(requestJson, Encoding.UTF8, "application/json");
                var httpResponse = await client.PostAsync("/api/chat", httpContent, ct);
                httpResponse.EnsureSuccessStatusCode();
                responseJson = await httpResponse.Content.ReadAsStringAsync(ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to call Ollama /api/chat");
                var errMsg = $"I'm sorry, I couldn't connect to the AI service: {ex.Message}";
                _history.Add(new OllamaMessage { Role = "assistant", Content = errMsg });
                return errMsg;
            }

            var response = JsonSerializer.Deserialize<OllamaChatResponse>(responseJson, SerializerOptions);
            if (response?.Message is null)
            {
                const string noResponseMsg = "I'm sorry, I didn't receive a valid response.";
                _history.Add(new OllamaMessage { Role = "assistant", Content = noResponseMsg });
                return noResponseMsg;
            }

            _history.Add(response.Message);

            if (response.Message.ToolCalls is null || response.Message.ToolCalls.Count == 0)
                return response.Message.Content ?? string.Empty;

            foreach (var toolCall in response.Message.ToolCalls)
            {
                var toolResult = await ExecuteToolAsync(toolCall, userId, ct);
                logger.LogDebug("Tool {Tool} returned: {Result}", toolCall.Function.Name,
                    toolResult[..Math.Min(200, toolResult.Length)]);
                _history.Add(new OllamaMessage { Role = "tool", Content = toolResult });
            }
        }

        const string maxIterMsg = "I apologize, I reached the maximum number of steps. Please try a simpler question.";
        _history.Add(new OllamaMessage { Role = "assistant", Content = maxIterMsg });
        return maxIterMsg;
    }

    private async Task<string> ExecuteToolAsync(OllamaToolCall toolCall, string userId, CancellationToken ct)
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
                "create_trip" => await CreateTripAsync(Str(args, "name")!, Str(args, "description"), Str(args, "start_date"), Str(args, "end_date"), userId),
                "update_trip" => await UpdateTripAsync(Str(args, "trip_id")!, Str(args, "name"), Str(args, "description"), Str(args, "start_date"), Str(args, "end_date"), userId),
                "delete_trip" => await DeleteTripAsync(Str(args, "trip_id")!, userId),
                "list_wishlists" => await ListWishlistsAsync(userId),
                "get_wishlist" => await GetWishlistAsync(Str(args, "wishlist_id")!, userId),
                "create_wishlist" => await CreateWishlistAsync(Str(args, "name")!, Str(args, "description"), userId),
                "update_wishlist" => await UpdateWishlistAsync(Str(args, "wishlist_id")!, Str(args, "name"), Str(args, "description"), userId),
                "delete_wishlist" => await DeleteWishlistAsync(Str(args, "wishlist_id")!, userId),
                "list_places" => await ListPlacesAsync(Str(args, "category"), userId),
                "get_place" => await GetPlaceAsync(Str(args, "place_id")!, userId),
                "create_place" => await CreatePlaceAsync(args, userId),
                "update_place" => await UpdatePlaceAsync(args, userId),
                "delete_place" => await DeletePlaceAsync(Str(args, "place_id")!, userId),
                _ => $"Unknown tool: {name}"
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Tool {Tool} failed", name);
            return $"Tool execution failed: {ex.Message}";
        }
    }

    private static string? Str(JsonElement args, string key)
    {
        if (args.ValueKind == JsonValueKind.Object &&
            args.TryGetProperty(key, out var val) &&
            val.ValueKind != JsonValueKind.Null)
            return val.GetString();
        return null;
    }

    private static bool TryGetDouble(JsonElement args, string key, out double value)
    {
        if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty(key, out var val))
        {
            if (val.ValueKind == JsonValueKind.Number) { value = val.GetDouble(); return true; }
            if (val.ValueKind == JsonValueKind.String && double.TryParse(val.GetString(), out value)) return true;
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
            t.Id, t.Name, t.Description,
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
            trip.Id, trip.Name, trip.Description,
            StartDate = trip.StartDate?.ToString("yyyy-MM-dd"),
            EndDate = trip.EndDate?.ToString("yyyy-MM-dd"),
            Days = trip.Days.OrderBy(d => d.DayNumber).Select(d => new
            {
                d.Id, d.DayNumber,
                Date = d.Date?.ToString("yyyy-MM-dd"),
                Places = d.Places.OrderBy(p => p.Order).Select(p => new
                {
                    TripPlaceId = p.Id, p.PlaceId,
                    PlaceName = p.Place?.Name,
                    ScheduledTime = p.ScheduledTime?.ToString("HH:mm"),
                    p.DurationMinutes, p.Notes, p.Order
                })
            }),
            UnscheduledPlaces = trip.UnscheduledPlaces.Select(p => new
            {
                TripPlaceId = p.Id, p.PlaceId, PlaceName = p.Place?.Name
            }),
            Accommodations = trip.Accommodations.Select(a => new
            {
                a.Id, a.Name, a.Address,
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
            StartDate = startDate is not null && DateTime.TryParse(startDate, out var sd) ? sd : null,
            EndDate = endDate is not null && DateTime.TryParse(endDate, out var ed) ? ed : null
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
        if (startDate is not null && DateTime.TryParse(startDate, out var sd)) trip.StartDate = sd;
        if (endDate is not null && DateTime.TryParse(endDate, out var ed)) trip.EndDate = ed;
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
            w.Id, w.Name, w.Description,
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
            wishlist.Id, wishlist.Name, wishlist.Description,
            CreatedAt = wishlist.CreatedAt.ToString("yyyy-MM-dd"),
            Places = wishlist.Places.Select(p => new
            {
                p.Id, p.Name, p.Category, p.Latitude, p.Longitude, p.Description,
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
            p.Id, p.Name, p.Category, p.Latitude, p.Longitude, p.Description,
            WishlistId = p.WishlistId, WishlistName = p.Wishlist?.Name,
            VisitDate = p.VisitDate?.ToString("yyyy-MM-dd"), Tags = p.Tags
        }));
    }

    private async Task<string> GetPlaceAsync(string placeId, string userId)
    {
        var place = await placeRepository.GetByIdAsync(placeId, userId);
        if (place is null) return "Place not found.";
        return JsonSerializer.Serialize(new
        {
            place.Id, place.Name, place.Category, place.Latitude, place.Longitude,
            place.Description, place.Notes, place.Url,
            WishlistId = place.WishlistId, WishlistName = place.Wishlist?.Name,
            VisitDate = place.VisitDate?.ToString("yyyy-MM-dd"), Tags = place.Tags
        });
    }

    private async Task<string> CreatePlaceAsync(JsonElement args, string userId)
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
            VisitDate = Str(args, "visit_date") is { } vd && DateTime.TryParse(vd, out var vdDt) ? vdDt : null,
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
        if (Str(args, "visit_date") is { } vd && DateTime.TryParse(vd, out var vdDt)) place.VisitDate = vdDt;
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
        await placeRepository.DeleteAsync(placeId);
        return "Place deleted successfully.";
    }

    // ── Tool definitions ─────────────────────────────────────────────────────────

    private static readonly object[] ToolDefinitions = BuildToolDefinitions();

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
        MakeTool("get_place", "Get details of a specific place by ID.",
            Props(("place_id", "string", "The place ID.")),
            ["place_id"]),
        MakeTool("create_place", "Create a new place in a wishlist.",
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
                    properties = properties ?? new Dictionary<string, object>(),
                    required = required ?? Array.Empty<string>()
                }
            }
        };
}
