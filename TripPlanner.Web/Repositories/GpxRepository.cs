using Microsoft.EntityFrameworkCore;
using TripPlanner.Web.Data;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public class GpxRepository : IGpxRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public GpxRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<GpxTrack>> GetAllAsync()
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.GpxTracks.AsNoTracking().Include(t => t.Points.OrderBy(x => x.Order)).ToListAsync();
    }

    public async Task<List<GpxTrack>> GetAllByUserAsync(string userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var userWishlistIds = context.UserWishlists
            .Where(w => w.UserId == userId)
            .Select(w => w.WishlistId);

        var userTripIds = context.Trips
            .Where(t => t.OwnerId == userId)
            .Select(t => t.Id)
            .Union(context.SharedTrips
                .Where(st => st.UserId == userId)
                .Select(st => st.TripId));

        var accessibleTrackIds = context.Places
            .Where(p => p.GpxTrackId != null && (
                (p.WishlistId != null && userWishlistIds.Contains(p.WishlistId)) ||
                (p.TripId != null && userTripIds.Contains(p.TripId))))
            .Select(p => p.GpxTrackId!)
            .Distinct();

        return await context.GpxTracks
            .AsNoTracking()
            .Where(t => accessibleTrackIds.Contains(t.Id))
            .Include(t => t.Points.OrderBy(x => x.Order))
            .ToListAsync();
    }


    public async Task<List<GpxTrack>> GetByTripIdAsync(string tripId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var trackIds = context.Trips.AsNoTracking().Where(t => t.Id == tripId)
            .SelectMany(t => t.Days.SelectMany(d => d.Places.Select(p => p.Place)))
            .Where(p => p != null && p.GpxTrackId != null)
            .Select(p => p!.GpxTrackId!);

        return await context.GpxTracks
            .AsNoTracking()
            .Where(x => trackIds.Contains(x.Id))
            .Include(t => t.Points.OrderBy(x => x.Order))
            .ToListAsync();
    }

    public async Task<GpxTrack?> GetByIdAsync(string id)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.GpxTracks.FindAsync(id);
    }

    public async Task<GpxTrack?> GetByIdWithPointsAsync(string id, string userId)
    {
        await using var context = _contextFactory.CreateDbContext();

        var userWishlistIds = context.UserWishlists
            .Where(w => w.UserId == userId)
            .Select(w => w.WishlistId);

        var userTripIds = context.Trips
            .Where(t => t.OwnerId == userId)
            .Select(t => t.Id)
            .Union(context.SharedTrips
                .Where(st => st.UserId == userId)
                .Select(st => st.TripId));

        var hasAccess = await context.Places
            .AnyAsync(p => p.GpxTrackId == id && (
                (p.WishlistId != null && userWishlistIds.Contains(p.WishlistId)) ||
                (p.TripId != null && userTripIds.Contains(p.TripId))));

        if (!hasAccess)
            return null;

        return await context.GpxTracks
            .AsNoTracking()
            .Include(t => t.Points.OrderBy(x => x.Order))
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<GpxTrack> AddAsync(GpxTrack track)
    {
        await using var context = _contextFactory.CreateDbContext();
        context.GpxTracks.Add(track);
        await context.SaveChangesAsync();
        return track;
    }

    public async Task DeleteAsync(string id, string userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        // Verify the user has owner-level access to the GPX track via its associated place
        var userWishlistIds = context.UserWishlists
            .Where(w => w.UserId == userId && w.Level == ShareLevel.Owner)
            .Select(w => w.WishlistId);

        var userOwnedTripIds = context.Trips
            .Where(t => t.OwnerId == userId)
            .Select(t => t.Id);

        var hasAccess = await context.Places
            .AnyAsync(p => p.GpxTrackId == id && (
                (p.WishlistId != null && userWishlistIds.Contains(p.WishlistId)) ||
                (p.TripId != null && userOwnedTripIds.Contains(p.TripId))));

        if (!hasAccess) return;

        var track = await context.GpxTracks.FindAsync(id);
        if (track != null)
        {
            context.GpxTracks.Remove(track);
            await context.SaveChangesAsync();
        }
    }
}
