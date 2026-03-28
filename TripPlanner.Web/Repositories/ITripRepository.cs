using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public interface ITripRepository
{
    Task<List<Trip>> GetAllAsync();
    Task<Trip?> GetByIdAsync(string id);
    Task<Trip> AddAsync(Trip trip);
    Task<TripPlace> AddTripPlaceAsync(TripPlace tripPlace);

    Task<Trip> UpdateAsync(Trip trip);
    Task DeleteAsync(string id, string userId);
    Task<List<Trip>> GetByOwnerAsync(string userId);
    Task<List<Trip>> GetSharedWithUserAsync(string userId);
    Task ShareWithUserAsync(string tripId, string userId);
    Task UnshareWithUserAsync(string tripId, string userId);
    Task<bool> CanUserAccessAsync(string tripId, string userId);

    Task<Accommodation> AddAccommodationAsync(Accommodation accommodation);
    Task<Accommodation> UpdateAccommodationAsync(Accommodation accommodation);
    Task DeleteAccommodationAsync(string accommodationId);
}
