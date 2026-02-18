using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public interface IPlaceRepository
{
    Task<List<Place>> GetAllAsync();
    Task<Place?> GetByIdAsync(string id);
    Task<Place> AddAsync(Place place);
    Task<Place> UpdateAsync(Place place);
    Task DeleteAsync(string id);
    Task<List<Place>> FilterAsync(PlaceCategory? category = null, List<string>? tags = null, bool? hasGpxTrack = null);
}
