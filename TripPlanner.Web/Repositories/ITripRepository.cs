using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public interface ITripRepository
{
    Task<List<Trip>> GetAllAsync();
    Task<Trip?> GetByIdAsync(string id);
    Task<Trip> AddAsync(Trip trip);
    Task<Trip> UpdateAsync(Trip trip);
    Task DeleteAsync(string id);
}
