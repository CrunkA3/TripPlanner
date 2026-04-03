using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public interface IPlaceCollectionRepository
{
    Task<List<PlaceCollection>> GetAllByOwnerAsync(string userId);
    Task<PlaceCollection?> GetByIdAsync(string id);
    Task<PlaceCollection?> GetByPublicTokenAsync(string token);
    Task<PlaceCollection> AddAsync(PlaceCollection collection);
    Task<PlaceCollection> UpdateAsync(PlaceCollection collection);
    Task DeleteAsync(string id, string userId);
    Task AddPlaceAsync(string collectionId, string placeId, string userId);
    Task RemovePlaceAsync(string collectionId, string placeId, string userId);
    Task<string?> GeneratePublicLinkAsync(string collectionId, string userId);
    Task RevokePublicLinkAsync(string collectionId, string userId);
    Task<List<Place>> GetPlacesAsync(string collectionId, string userId);
    Task<List<Place>> GetPlacesByPublicTokenAsync(string token);
}
