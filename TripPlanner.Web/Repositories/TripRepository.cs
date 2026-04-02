using Microsoft.EntityFrameworkCore;
using TripPlanner.Web.Data;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public class TripRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : ITripRepository
{
    public async Task<List<Trip>> GetAllAsync()
    {
        await using var context = contextFactory.CreateDbContext();
        return await WithStandardIncludes(context.Trips.AsNoTracking())
            .Include(t => t.SharedWith)
            .ToListAsync();
    }

    public async Task<Trip?> GetByIdAsync(string id)
    {
        await using var context = contextFactory.CreateDbContext();
        return await WithStandardIncludes(context.Trips.AsNoTracking())
            .Include(t => t.SharedWith)
                .ThenInclude(st => st.User)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Trip> AddAsync(Trip trip)
    {
        await using var context = contextFactory.CreateDbContext();
        context.Trips.Add(trip);
        await context.SaveChangesAsync();
        return trip;
    }
    public async Task<TripPlace> AddTripPlaceAsync(TripPlace tripPlace)
    {
        await using var context = contextFactory.CreateDbContext();
        context.Entry(tripPlace).State = EntityState.Added;
        await context.SaveChangesAsync();
        return tripPlace;
    }


    public async Task<Trip> UpdateAsync(Trip trip)
    {
        await using var context = contextFactory.CreateDbContext();
        trip.UpdatedAt = DateTimeOffset.UtcNow;
        context.Entry(trip).State = EntityState.Modified;
        await context.SaveChangesAsync();
        return trip;
    }

    public async Task<int> UpdateTipPlaceDateTimeAsync(TripPlace tripPlace, DateTimeOffset? scheduledTime, string userId)
    {
        await using var context = contextFactory.CreateDbContext();
        var tripFound = await context.Trips
            .Include(t => t.Days)
            .ThenInclude(d => d.Places)
            .Where(t => t.OwnerId == userId && t.Days.Any(d => d.Places.Any(p => p.Id == tripPlace.Id)))
            .AnyAsync();

        if (!tripFound) throw new KeyNotFoundException($"TripPlace with id {tripPlace.Id} not found for user {userId}");

        return await context.TripPlaces
            .Where(tp => tp.Id == tripPlace.Id)
            .ExecuteUpdateAsync(tp => tp.SetProperty(p => p.ScheduledTime, scheduledTime));
    }


    public async Task DeleteAsync(string id, string userId)
    {
        await using var context = contextFactory.CreateDbContext();
        var trip = await context.Trips.FirstOrDefaultAsync(t => t.Id == id && t.OwnerId == userId);
        if (trip != null)
        {
            context.Trips.Remove(trip);
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteTripPlaceAsync(string id, string userId)
    {
        await using var context = contextFactory.CreateDbContext();
        var tripFound = await context.Trips
            .Include(t => t.Days)
            .ThenInclude(d => d.Places)
            .Where(t => t.OwnerId == userId && t.Days.Any(d => d.Places.Any(p => p.Id == id)))
            .AnyAsync();

        if (!tripFound) throw new KeyNotFoundException($"Trip with id {id} not found for user {userId}");

        await context.TripPlaces
            .Where(tp => tp.Id == id)
            .ExecuteDeleteAsync();
    }


    public async Task<List<Trip>> GetByOwnerAsync(string userId)
    {
        await using var context = contextFactory.CreateDbContext();
        return await WithStandardIncludes(context.Trips.AsNoTracking())
            .Where(t => t.OwnerId == userId)
            .ToListAsync();
    }

    public async Task<List<Trip>> GetSharedWithUserAsync(string userId)
    {
        await using var context = contextFactory.CreateDbContext();
        return await context.SharedTrips
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
        await using var context = contextFactory.CreateDbContext();
        var existing = await context.SharedTrips
            .FirstOrDefaultAsync(st => st.TripId == tripId && st.UserId == userId);

        if (existing == null)
        {
            context.SharedTrips.Add(new SharedTrip
            {
                TripId = tripId,
                UserId = userId
            });
            await context.SaveChangesAsync();
        }
    }

    public async Task UnshareWithUserAsync(string tripId, string userId)
    {
        await using var context = contextFactory.CreateDbContext();
        var sharedTrip = await context.SharedTrips
            .FirstOrDefaultAsync(st => st.TripId == tripId && st.UserId == userId);

        if (sharedTrip != null)
        {
            context.SharedTrips.Remove(sharedTrip);
            await context.SaveChangesAsync();
        }
    }

    public async Task<bool> CanUserAccessAsync(string tripId, string userId)
    {
        await using var context = contextFactory.CreateDbContext();
        return await context.Trips
            .AnyAsync(t => t.Id == tripId && (t.OwnerId == userId
                || t.SharedWith.Any(st => st.UserId == userId)));
    }

    public async Task<Accommodation> AddAccommodationAsync(Accommodation accommodation)
    {
        await using var context = contextFactory.CreateDbContext();
        context.Accommodations.Add(accommodation);
        await context.SaveChangesAsync();
        return accommodation;
    }

    public async Task<Accommodation> UpdateAccommodationAsync(Accommodation accommodation)
    {
        await using var context = contextFactory.CreateDbContext();
        accommodation.UpdatedAt = DateTimeOffset.UtcNow;
        context.Accommodations.Update(accommodation);
        await context.SaveChangesAsync();
        return accommodation;
    }

    public async Task DeleteAccommodationAsync(string accommodationId)
    {
        await using var context = contextFactory.CreateDbContext();
        var accommodation = await context.Accommodations.FindAsync(accommodationId);
        if (accommodation != null)
        {
            context.Accommodations.Remove(accommodation);
            await context.SaveChangesAsync();
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
