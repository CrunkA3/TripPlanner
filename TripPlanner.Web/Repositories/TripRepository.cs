using Microsoft.EntityFrameworkCore;
using TripPlanner.Web.Data;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public class TripRepository(ApplicationDbContext context) : ITripRepository
{
    private readonly ApplicationDbContext _context = context;

    public async Task<List<Trip>> GetAllAsync()
    {
        return await WithStandardIncludes(_context.Trips.AsNoTracking())
            .Include(t => t.SharedWith)
            .ToListAsync();
    }

    public async Task<Trip?> GetByIdAsync(string id)
    {
        return await WithStandardIncludes(_context.Trips.AsNoTracking())
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
        trip.UpdatedAt = DateTimeOffset.UtcNow;
        var tripPlaces = trip.Days.SelectMany(d => d.Places).ToList();

        var states = tripPlaces.Select(tp => _context.Entry(tp).State);
        var entry = _context.Entry(trip);
        _context.Entry(trip).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return trip;
    }

    public async Task DeleteAsync(string id, string userId)
    {
        var trip = await _context.Trips.FirstOrDefaultAsync(t => t.Id == id && t.OwnerId == userId);
        if (trip != null)
        {
            _context.Trips.Remove(trip);
            await _context.SaveChangesAsync();
        }
    }


    public async Task<TripPlace> AddTripPlaceAsync(TripPlace tripPlace)
    {
        _context.Entry(tripPlace).State = EntityState.Added;
        await _context.SaveChangesAsync();
        return tripPlace;
    }


    public async Task<List<Trip>> GetByOwnerAsync(string userId)
    {
        return await WithStandardIncludes(_context.Trips.AsNoTracking())
            .Where(t => t.OwnerId == userId)
            .ToListAsync();
    }

    public async Task<List<Trip>> GetSharedWithUserAsync(string userId)
    {
        return await _context.SharedTrips
            .AsNoTracking()
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
            .AnyAsync(t => t.Id == tripId && (t.OwnerId == userId
                || t.SharedWith.Any(st => st.UserId == userId)));
    }

    public async Task<Accommodation> AddAccommodationAsync(Accommodation accommodation)
    {
        _context.Accommodations.Add(accommodation);
        await _context.SaveChangesAsync();
        return accommodation;
    }

    public async Task<Accommodation> UpdateAccommodationAsync(Accommodation accommodation)
    {
        accommodation.UpdatedAt = DateTimeOffset.UtcNow;
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

    private static IQueryable<Trip> WithStandardIncludes(IQueryable<Trip> query) =>
        query
            .Include(t => t.Days)
                .ThenInclude(d => d.Places)
                    .ThenInclude(p => p.Place)
            .Include(t => t.UnscheduledPlaces)
                .ThenInclude(p => p.Place)
            .Include(t => t.Accommodations);
}
