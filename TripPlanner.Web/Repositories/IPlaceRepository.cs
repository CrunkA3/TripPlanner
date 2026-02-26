using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public interface IPlaceRepository
{
    Task<List<Place>> GetAllByUserAsync(string userId);
    Task<List<Place>> GetAllWithAnyWishlistAsync(string userId);
    Task<List<Place>> GetAllForTripAsync(string tripId);


    Task<Place?> GetByIdAsync(string id, string userId);
    Task<Place> AddAsync(Place place);
    Task<Place> UpdateAsync(Place place);
    Task DeleteAsync(string id);
    Task<List<Place>> FilterAsync(PlaceCategory? category = null, List<string>? tags = null, bool? hasGpxTrack = null);
    Task<List<Place>> GetByWishlistIdAsync(string wishlistId);
}
