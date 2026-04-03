using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public interface IGpxRepository
{
    Task<List<GpxTrack>> GetAllAsync();
    Task<List<GpxTrack>> GetAllByUserAsync(string userId);
    Task<List<GpxTrack>> GetByTripIdAsync(string tripId);
    Task<GpxTrack?> GetByIdAsync(string id);
    Task<GpxTrack?> GetByIdWithPointsAsync(string id, string userId);
    Task<GpxTrack> AddAsync(GpxTrack track);
    Task DeleteAsync(string id, string userId);
}
