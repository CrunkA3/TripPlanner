using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public interface IWishlistRepository
{
    Task<List<Wishlist>> GetAllByUserAsync(string userId);
    Task<Wishlist?> GetByIdAsync(string id);
    Task<Wishlist> AddAsync(Wishlist wishlist);
    Task<Wishlist> UpdateAsync(Wishlist wishlist);
    Task DeleteAsync(string id);
    Task ShareWithUserAsync(string wishlistId, string userId);
    Task UnshareWithUserAsync(string wishlistId, string userId);
    Task<bool> CanUserAccessAsync(string wishlistId, string userId);
    Task<bool> CanUserEditAsync(string wishlistId, string userId);
    Task<bool> CanUserAdministrateAsync(string wishlistId, string userId);

}
