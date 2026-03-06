using System.ComponentModel;
using System.Security.Claims;
using System.Text.Json;
using ModelContextProtocol.Server;
using TripPlanner.Web.Models;
using TripPlanner.Web.Repositories;

namespace TripPlanner.Web.McpTools;

[McpServerToolType]
public class PlaceMcpTools(IPlaceRepository placeRepository, IHttpContextAccessor httpContextAccessor)
{
    private string? UserId => httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    [McpServerTool, Description("List all places accessible to the current user. Optionally filter by category.")]
    public async Task<string> ListPlaces(
        [Description("Optional category filter: Viewpoint, Museum, Restaurant, Nature, Activity, Accommodation, Shopping, Entertainment, Race, Other.")] string? category = null)
    {
        if (UserId is null) return "Unauthorized.";

        // Always retrieve places scoped to the current user first, then optionally filter by category
        var places = await placeRepository.GetAllByUserAsync(UserId);

        if (category is not null && Enum.TryParse<PlaceCategory>(category, true, out var cat))
            places = places.Where(p => p.Category == cat).ToList();

        var result = places.Select(p => new
        {
            p.Id,
            p.Name,
            p.Category,
            p.Latitude,
            p.Longitude,
            p.Description,
            p.Notes,
            p.Url,
            WishlistId = p.WishlistId,
            WishlistName = p.Wishlist?.Name,
            VisitDate = p.VisitDate?.ToString("yyyy-MM-dd"),
            Tags = p.Tags
        });

        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Get the details of a specific place by ID.")]
    public async Task<string> GetPlace([Description("The place ID.")] string placeId)
    {
        if (UserId is null) return "Unauthorized.";

        var place = await placeRepository.GetByIdAsync(placeId, UserId);
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
            VisitDateEnd = place.VisitDateEnd?.ToString("yyyy-MM-dd"),
            Tags = place.Tags,
            HasGpxTrack = place.HasGpxTrack
        });
    }

    [McpServerTool, Description("Create a new place in a wishlist.")]
    public async Task<string> CreatePlace(
        [Description("The name of the place.")] string name,
        [Description("The category: Viewpoint, Museum, Restaurant, Nature, Activity, Accommodation, Shopping, Entertainment, Race, Other.")] string category,
        [Description("Latitude coordinate.")] double latitude,
        [Description("Longitude coordinate.")] double longitude,
        [Description("The wishlist ID to add this place to.")] string wishlistId,
        [Description("Optional description.")] string? description = null,
        [Description("Optional notes.")] string? notes = null,
        [Description("Optional URL with more information.")] string? url = null,
        [Description("Optional visit date in yyyy-MM-dd format.")] string? visitDate = null,
        [Description("Optional comma-separated tags.")] string? tags = null)
    {
        if (UserId is null) return "Unauthorized.";

        if (!Enum.TryParse<PlaceCategory>(category, true, out var cat))
            return $"Invalid category '{category}'. Valid values: {string.Join(", ", Enum.GetNames<PlaceCategory>())}";

        var place = new Models.Place
        {
            Name = name,
            Category = cat,
            Latitude = latitude,
            Longitude = longitude,
            WishlistId = wishlistId,
            Description = description ?? string.Empty,
            Notes = notes,
            Url = url,
            VisitDate = visitDate is not null && DateTime.TryParse(visitDate, out var vd) ? vd : null,
            Tags = tags?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() ?? []
        };

        var created = await placeRepository.AddAsync(place);
        return JsonSerializer.Serialize(new { created.Id, created.Name, Message = "Place created successfully." });
    }

    [McpServerTool, Description("Update an existing place's details.")]
    public async Task<string> UpdatePlace(
        [Description("The ID of the place to update.")] string placeId,
        [Description("New name (optional).")] string? name = null,
        [Description("New description (optional).")] string? description = null,
        [Description("New notes (optional).")] string? notes = null,
        [Description("New URL (optional).")] string? url = null,
        [Description("New visit date yyyy-MM-dd (optional).")] string? visitDate = null,
        [Description("New comma-separated tags (optional).")] string? tags = null)
    {
        if (UserId is null) return "Unauthorized.";

        var place = await placeRepository.GetByIdAsync(placeId, UserId);
        if (place is null) return "Place not found.";

        if (name is not null) place.Name = name;
        if (description is not null) place.Description = description;
        if (notes is not null) place.Notes = notes;
        if (url is not null) place.Url = url;
        if (visitDate is not null) place.VisitDate = DateTime.TryParse(visitDate, out var vd) ? vd : place.VisitDate;
        if (tags is not null) place.Tags = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        place.UpdatedAt = DateTime.UtcNow;
        await placeRepository.UpdateAsync(place);
        return "Place updated successfully.";
    }

    [McpServerTool, Description("Delete a place.")]
    public async Task<string> DeletePlace([Description("The ID of the place to delete.")] string placeId)
    {
        if (UserId is null) return "Unauthorized.";

        var place = await placeRepository.GetByIdAsync(placeId, UserId);
        if (place is null) return "Place not found.";

        await placeRepository.DeleteAsync(placeId);
        return "Place deleted successfully.";
    }
}
