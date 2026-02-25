using Microsoft.EntityFrameworkCore;
using TripPlanner.Web.Data;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public class EfTripRepository : ITripRepository
{
    private readonly ApplicationDbContext _context;

    public EfTripRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Trip>> GetAllAsync()
    {
        return await _context.Trips
            .Include(t => t.Days)
                .ThenInclude(d => d.Places)
                    .ThenInclude(p => p.Place)
            .Include(t => t.UnscheduledPlaces)
                .ThenInclude(p => p.Place)
            .Include(t => t.Accommodations)
            .Include(t => t.SharedWith)
            .ToListAsync();
    }

    public async Task<Trip?> GetByIdAsync(string id)
    {
        return await _context.Trips
            .Include(t => t.Days)
                .ThenInclude(d => d.Places)
                    .ThenInclude(p => p.Place)
            .Include(t => t.UnscheduledPlaces)
                .ThenInclude(p => p.Place)
            .Include(t => t.Accommodations)
            .Include(t => t.SharedWith)
                .ThenInclude(st => st.User)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Trip> AddAsync(Trip trip)
    {
        _context.Trips.Add(trip);
        await _context.SaveChangesAsync();
        return trip;
    }

    public async Task<Trip> UpdateAsync(Trip trip)
    {
        trip.UpdatedAt = DateTime.UtcNow;
        _context.Entry(trip).CurrentValues.SetValues(trip);
        await _context.SaveChangesAsync();
        return trip;
    }

    public async Task DeleteAsync(string id)
    {
        var trip = await _context.Trips.FindAsync(id);
        if (trip != null)
        {
            _context.Trips.Remove(trip);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<Trip>> GetByOwnerAsync(string userId)
    {
        return await _context.Trips
            .Where(t => t.OwnerId == userId)
            .Include(t => t.Days)
                .ThenInclude(d => d.Places)
                    .ThenInclude(p => p.Place)
            .Include(t => t.UnscheduledPlaces)
                .ThenInclude(p => p.Place)
            .Include(t => t.Accommodations)
            .ToListAsync();
    }

    public async Task<List<Trip>> GetSharedWithUserAsync(string userId)
    {
        return await _context.SharedTrips
            .Where(st => st.UserId == userId)
            .Include(st => st.Trip)
                .ThenInclude(t => t!.Days)
                    .ThenInclude(d => d.Places)
                        .ThenInclude(p => p.Place)
            .Include(st => st.Trip)
                .ThenInclude(t => t!.UnscheduledPlaces)
                    .ThenInclude(p => p.Place)
            .Select(st => st.Trip!)
            .ToListAsync();
    }

    public async Task ShareWithUserAsync(string tripId, string userId)
    {
        var existing = await _context.SharedTrips
            .FirstOrDefaultAsync(st => st.TripId == tripId && st.UserId == userId);

        if (existing == null)
        {
            _context.SharedTrips.Add(new SharedTrip
            {
                TripId = tripId,
                UserId = userId
            });
            await _context.SaveChangesAsync();
        }
    }

    public async Task UnshareWithUserAsync(string tripId, string userId)
    {
        var sharedTrip = await _context.SharedTrips
            .FirstOrDefaultAsync(st => st.TripId == tripId && st.UserId == userId);

        if (sharedTrip != null)
        {
            _context.SharedTrips.Remove(sharedTrip);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> CanUserAccessAsync(string tripId, string userId)
    {
        return await _context.Trips
            .AnyAsync(t => t.Id == tripId && t.OwnerId == userId)
            || await _context.SharedTrips
                .AnyAsync(st => st.TripId == tripId && st.UserId == userId);
    }

    public async Task<Accommodation> AddAccommodationAsync(Accommodation accommodation)
    {
        _context.Accommodations.Add(accommodation);
        await _context.SaveChangesAsync();
        return accommodation;
    }

    public async Task<Accommodation> UpdateAccommodationAsync(Accommodation accommodation)
    {
        accommodation.UpdatedAt = DateTime.UtcNow;
        _context.Accommodations.Update(accommodation);
        await _context.SaveChangesAsync();
        return accommodation;
    }

    public async Task DeleteAccommodationAsync(string accommodationId)
    {
        var accommodation = await _context.Accommodations.FindAsync(accommodationId);
        if (accommodation != null)
        {
            _context.Accommodations.Remove(accommodation);
            await _context.SaveChangesAsync();
        }
    }
}
