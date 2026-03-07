using System.ComponentModel;
using System.Security.Claims;
using System.Text.Json;
using ModelContextProtocol.Server;
using TripPlanner.Web.Models;
using TripPlanner.Web.Repositories;

namespace TripPlanner.Web.McpTools;

[McpServerToolType]
public class WishlistMcpTools(IWishlistRepository wishlistRepository, IHttpContextAccessor httpContextAccessor)
{
    private string? UserId => httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    [McpServerTool, Description("List all wishlists accessible to the current user.")]
    public async Task<string> ListWishlists()
    {
        if (UserId is null) return "Unauthorized.";

        var wishlists = await wishlistRepository.GetAllByUserAsync(UserId);

        var result = wishlists.Select(w => new
        {
            w.Id,
            w.Name,
            w.Description,
            PlaceCount = w.Places.Count,
            CreatedAt = w.CreatedAt.ToString("yyyy-MM-dd")
        });

        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Get the details of a wishlist including its places.")]
    public async Task<string> GetWishlist([Description("The wishlist ID.")] string wishlistId)
    {
        if (UserId is null) return "Unauthorized.";

        if (!await wishlistRepository.CanUserAccessAsync(wishlistId, UserId))
            return "Wishlist not found or access denied.";

        var wishlist = await wishlistRepository.GetByIdAsync(wishlistId);
        if (wishlist is null) return "Wishlist not found.";

        var result = new
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
                p.Notes,
                p.Url,
                VisitDate = p.VisitDate?.ToString("yyyy-MM-dd")
            })
        };

        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Create a new wishlist.")]
    public async Task<string> CreateWishlist(
        [Description("The name of the wishlist.")] string name,
        [Description("An optional description.")] string? description = null)
    {
        if (UserId is null) return "Unauthorized.";

        var wishlist = new Wishlist
        {
            Name = name,
            Description = description,
            UpdatedAt = DateTime.UtcNow
        };
        // Share with owner
        wishlist.SharedWith.Add(new UserWishlist
        {
            UserId = UserId,
            WishlistId = wishlist.Id,
            Level = ShareLevel.Owner
        });

        var created = await wishlistRepository.AddAsync(wishlist);
        return JsonSerializer.Serialize(new { created.Id, created.Name, Message = "Wishlist created successfully." });
    }

    [McpServerTool, Description("Update the name or description of an existing wishlist.")]
    public async Task<string> UpdateWishlist(
        [Description("The ID of the wishlist to update.")] string wishlistId,
        [Description("New name (optional).")] string? name = null,
        [Description("New description (optional).")] string? description = null)
    {
        if (UserId is null) return "Unauthorized.";

        if (!await wishlistRepository.CanUserEditAsync(wishlistId, UserId))
            return "Wishlist not found or access denied.";

        var wishlist = await wishlistRepository.GetByIdAsync(wishlistId);
        if (wishlist is null) return "Wishlist not found.";

        if (name is not null) wishlist.Name = name;
        if (description is not null) wishlist.Description = description;

        await wishlistRepository.UpdateAsync(wishlist);
        return "Wishlist updated successfully.";
    }

    [McpServerTool, Description("Delete a wishlist.")]
    public async Task<string> DeleteWishlist([Description("The ID of the wishlist to delete.")] string wishlistId)
    {
        if (UserId is null) return "Unauthorized.";

        if (!await wishlistRepository.CanUserAdministrateAsync(wishlistId, UserId))
            return "Wishlist not found or access denied.";

        await wishlistRepository.DeleteAsync(wishlistId);
        return "Wishlist deleted successfully.";
    }
}
