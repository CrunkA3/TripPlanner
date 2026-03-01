using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public interface IGpxRepository
{
    Task<List<GpxTrack>> GetAllAsync();
    Task<List<GpxTrack>> GetByTripIdAsync(string tripId);
    Task<GpxTrack?> GetByIdAsync(string id);
    Task<GpxTrack> AddAsync(GpxTrack track);
    Task DeleteAsync(string id);
}
