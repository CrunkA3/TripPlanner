using Microsoft.EntityFrameworkCore;
using TripPlanner.Web.Data;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public class GpxRepository : IGpxRepository
{
    private readonly ApplicationDbContext _context;

    public GpxRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<GpxTrack>> GetAllAsync()
    {
        return await _context.GpxTracks.AsNoTracking().Include(t => t.Points.OrderBy(x => x.Order)).ToListAsync();
    }

    public async Task<List<GpxTrack>> GetAllByUserAsync(string userId)
    {
        var userWishlistIds = _context.UserWishlists
            .Where(w => w.UserId == userId)
            .Select(w => w.WishlistId);

        var userTripIds = _context.Trips
            .Where(t => t.OwnerId == userId)
            .Select(t => t.Id)
            .Union(_context.SharedTrips
                .Where(st => st.UserId == userId)
                .Select(st => st.TripId));

        var accessibleTrackIds = _context.Places
            .Where(p => p.GpxTrackId != null && (
                (p.WishlistId != null && userWishlistIds.Contains(p.WishlistId)) ||
                (p.TripId != null && userTripIds.Contains(p.TripId))))
            .Select(p => p.GpxTrackId!)
            .Distinct();

        return await _context.GpxTracks
            .AsNoTracking()
            .Where(t => accessibleTrackIds.Contains(t.Id))
            .Include(t => t.Points.OrderBy(x => x.Order))
            .ToListAsync();
    }


    public async Task<List<GpxTrack>> GetByTripIdAsync(string tripId)
    {
        var trackIds = _context.Trips.AsNoTracking().Where(t => t.Id == tripId)
            .SelectMany(t => t.Days.SelectMany(d => d.Places.Select(p => p.Place)))
            .Where(p => p != null && p.GpxTrackId != null)
            .Select(p => p!.GpxTrackId!);

        return await _context.GpxTracks
            .AsNoTracking()
            .Where(x => trackIds.Contains(x.Id))
            .Include(t => t.Points.OrderBy(x => x.Order))
            .ToListAsync();
    }

    public async Task<GpxTrack?> GetByIdAsync(string id)
    {
        return await _context.GpxTracks.FindAsync(id);
    }

    public async Task<GpxTrack> AddAsync(GpxTrack track)
    {
        _context.GpxTracks.Add(track);
        await _context.SaveChangesAsync();
        return track;
    }

    public async Task DeleteAsync(string id, string userId)
    {
        // Verify the user has owner-level access to the GPX track via its associated place
        var userWishlistIds = _context.UserWishlists
            .Where(w => w.UserId == userId && w.Level == ShareLevel.Owner)
            .Select(w => w.WishlistId);

        var userOwnedTripIds = _context.Trips
            .Where(t => t.OwnerId == userId)
            .Select(t => t.Id);

        var hasAccess = await _context.Places
            .AnyAsync(p => p.GpxTrackId == id && (
                (p.WishlistId != null && userWishlistIds.Contains(p.WishlistId)) ||
                (p.TripId != null && userOwnedTripIds.Contains(p.TripId))));

        if (!hasAccess) return;

        var track = await _context.GpxTracks.FindAsync(id);
        if (track != null)
        {
            _context.GpxTracks.Remove(track);
            await _context.SaveChangesAsync();
        }
    }
}
