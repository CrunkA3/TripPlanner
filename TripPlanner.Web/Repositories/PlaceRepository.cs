using Microsoft.EntityFrameworkCore;
using TripPlanner.Web.Data;
using TripPlanner.Web.Models;
using TripPlanner.Web.Services;

namespace TripPlanner.Web.Repositories;

public class PlaceRepository : IPlaceRepository
{
    private readonly ApplicationDbContext _context;

    public PlaceRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Place>> GetAllByUserAsync(string userId)
    {
        var userWishlists = _context.UserWishlists
            .Where(w => w.UserId == userId);

        var tripPlaces = _context.Trips
            .Include(t => t.Days)
            .ThenInclude(td => td.Places)
            .Where(t => t.OwnerId == userId)
            .SelectMany(t => t.Days.SelectMany(d => d.Places.Select(tp => tp.PlaceId)));

        var query = _context.Places
            .Include(p => p.Wishlist)
            .Where(p => p.Wishlist != null && (
                            userWishlists.Any(swl => swl.WishlistId == p.WishlistId) ||
                            tripPlaces.Any(placeId => placeId == p.Id)))
            .Include(p => p.Trip);

        return await query.ToListAsync();
    }


    public async Task<List<Place>> GetAllWithAnyWishlistAsync(string userId)
    {
        return await _context.Places
            .Where(p => p.WishlistId != null)
            .Include(p => p.Wishlist)
            .ToListAsync();
    }

    public async Task<List<Place>> GetAllForTripAsync(string tripId)
    {
        return await _context.Places
            .Where(p => p.TripId == tripId)
            .Include(p => p.Wishlist)
            .ToListAsync();
    }

    public async Task<Place?> GetByIdAsync(string id, string userId)
    {
        return await _context.Places
            .Include(p => p.Wishlist)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Place> AddAsync(Place place)
    {
        _context.Places.Add(place);
        await _context.SaveChangesAsync();
        return place;
    }

    public async Task<Place> UpdateAsync(Place place)
    {
        place.UpdatedAt = DateTime.UtcNow;
        _context.Entry(place).CurrentValues.SetValues(place);
        await _context.SaveChangesAsync();
        return place;
    }

    public async Task DeleteAsync(string id)
    {
        var place = await _context.Places.FindAsync(id);
        if (place != null)
        {
            _context.Places.Remove(place);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<Place>> FilterAsync(PlaceCategory? category = null, List<string>? tags = null, bool? hasGpxTrack = null)
    {
        var query = _context.Places.AsQueryable();

        if (category.HasValue)
        {
            query = query.Where(p => p.Category == category.Value);
        }

        if (tags != null && tags.Count != 0)
        {
            foreach (var tag in tags)
            {
                query = query.Where(p => p.Tags.Contains(tag));
            }
        }

        if (hasGpxTrack.HasValue)
        {
            query = hasGpxTrack.Value
                ? query.Where(p => p.GpxTrackId != null)
                : query.Where(p => p.GpxTrackId == null);
        }

        return await query.Include(p => p.Wishlist).ToListAsync();
    }

    public async Task<List<Place>> GetByWishlistIdAsync(string wishlistId)
    {
        return await _context.Places
            .Where(p => p.WishlistId == wishlistId)
            .ToListAsync();
    }
}
