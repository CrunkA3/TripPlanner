using Microsoft.EntityFrameworkCore;
using TripPlanner.Web.Data;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public class PlaceRepository : IPlaceRepository
{
    private readonly ApplicationDbContext _context;

    public PlaceRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    private IQueryable<string> GetUserWishlistIds(string userId) =>
        _context.UserWishlists
            .Where(w => w.UserId == userId)
            .Select(w => w.WishlistId);

    private IQueryable<string> GetUserTripIds(string userId) =>
        _context.Trips
            .Where(t => t.OwnerId == userId)
            .Select(t => t.Id)
            .Union(_context.SharedTrips
                .Where(st => st.UserId == userId)
                .Select(st => st.TripId));

    private IQueryable<string> GetOwnedTripPlaceIds(string userId) =>
        _context.Trips
            .Where(t => t.OwnerId == userId)
            .SelectMany(t => t.Days.SelectMany(d => d.Places.Select(tp => tp.PlaceId)));

    public async Task<List<Place>> GetAllByUserAsync(string userId)
    {
        var userWishlistIds = GetUserWishlistIds(userId);
        var tripPlaceIds = GetOwnedTripPlaceIds(userId);

        var query = _context.Places
            .AsNoTracking()
            .Include(p => p.Wishlist)
            .Where(p => p.Wishlist != null && (
                            userWishlistIds.Contains(p.WishlistId!) ||
                            tripPlaceIds.Contains(p.Id)))
            .Include(p => p.Trip)
            .Select(p => new
            {
                Place = p,
                ImageIds = p.Images.Select(i => i.Id)
            });

        var queryResult = await query.ToListAsync();

        return [.. queryResult.Select(q =>
        {
            q.Place.ImageIds = [.. q.ImageIds];
            return q.Place;
        })];
    }


    public async Task<List<Place>> GetAllWithAnyWishlistAsync(string userId)
    {
        var userWishlistIds = GetUserWishlistIds(userId);

        var query = _context.Places
            .AsNoTracking()
            .Where(p => p.WishlistId != null && userWishlistIds.Contains(p.WishlistId))
            .Include(p => p.Wishlist)
            .ThenInclude(wl => wl!.SharedWith)
            .Select(p => new
            {
                Place = p,
                ImageIds = p.Images.Select(i => i.Id)
            });

        var queryResult = await query.ToListAsync();

        return [.. queryResult.Select(q =>
        {
            q.Place.ImageIds = [.. q.ImageIds];
            return q.Place;
        })];
    }

    public async Task<List<Place>> GetAllForTripAsync(string tripId)
    {
        var query = _context.Places
            .AsNoTracking()
            .Where(p => p.TripId == tripId)
            .Include(p => p.Wishlist)
            .Select(p => new
            {
                Place = p,
                ImageIds = p.Images.Select(i => i.Id)
            }); ;

        var queryResult = await query.ToListAsync();

        return [.. queryResult.Select(q =>
        {
            q.Place.ImageIds = [.. q.ImageIds];
            return q.Place;
        })];
    }

    public async Task<Place?> GetByIdAsync(string id, string userId)
    {
        var userWishlistIds = GetUserWishlistIds(userId);
        var userTripIds = GetUserTripIds(userId);

        var query = _context.Places
            .AsNoTracking()
            .Include(p => p.Wishlist)
            .Select(p => new
            {
                Place = p,
                ImageIds = p.Images.Select(i => i.Id)
            });

        var queryResult = await query
            .FirstOrDefaultAsync(p => p.Place.Id == id && (
                (p.Place.WishlistId != null && userWishlistIds.Contains(p.Place.WishlistId)) ||
                (p.Place.TripId != null && userTripIds.Contains(p.Place.TripId))));

        queryResult?.Place.ImageIds = [.. queryResult?.ImageIds ?? []];

        return queryResult?.Place;
    }

    public async Task<bool> ExistsAsync(string id)
    {
        return await _context.Places.AnyAsync(p => p.Id == id);
    }

    public async Task<bool> HasAccessAsync(string id, string userId)
    {
        var userWishlistIds = GetUserWishlistIds(userId);
        var userTripIds = GetUserTripIds(userId);

        return await _context.Places.AnyAsync(p => p.Id == id && (
            (p.WishlistId != null && userWishlistIds.Contains(p.WishlistId)) ||
            (p.TripId != null && userTripIds.Contains(p.TripId))));
    }

    public async Task<Place> AddAsync(Place place)
    {
        _context.Places.Add(place);
        await _context.SaveChangesAsync();

        place.ImageIds = [.. place.Images.Select(i => i.Id)];

        return place;
    }

    public async Task<Place> UpdateAsync(Place place)
    {
        place.UpdatedAt = DateTimeOffset.UtcNow;

        // Sync images: load existing, add new, update existing, remove deleted
        var existingImages = await _context.PlaceImages
            .AsNoTracking()
            .Where(pi => pi.PlaceId == place.Id)
            .ToListAsync();

        var newImageIds = place.Images.Select(i => i.Id).ToHashSet();
        var toRemove = existingImages.Where(ei => !newImageIds.Contains(ei.Id)).ToList();
        _context.PlaceImages.RemoveRange(toRemove);

        var existingImageIds = existingImages.Select(i => i.Id).ToHashSet();
        foreach (var img in place.Images)
        {
            img.PlaceId = place.Id;
            if (!existingImageIds.Contains(img.Id))
            {
                _context.PlaceImages.Add(img);
            }
            else
            {
                _context.Entry(img).State = EntityState.Modified;
            }
        }

        _context.Entry(place).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return place;
    }

    public async Task<bool> MarkAsReviewedAsync(string id, string userId)
    {
        var userWishlistIds = GetUserWishlistIds(userId);
        var userTripIds = GetUserTripIds(userId);

        var updatedAt = DateTimeOffset.UtcNow;
        var count = await _context.Places
            .Where(p => p.Id == id && (
                (p.WishlistId != null && userWishlistIds.Contains(p.WishlistId)) ||
                (p.TripId != null && userTripIds.Contains(p.TripId))))
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.NeedsReview, false)
                .SetProperty(p => p.UpdatedAt, updatedAt));

        return count > 0;
    }

    public async Task DeleteAsync(string id, string userId)
    {
        var place = await GetByIdAsync(id, userId);
        if (place != null)
        {
            _context.Places.Remove(place);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<Place>> FilterAsync(PlaceCategory? category = null, List<string>? tags = null, bool? hasGpxTrack = null)
    {
        var query = _context.Places.AsNoTracking();

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



        var queryWithImageIds = query
            .Include(p => p.Wishlist)
            .Select(p => new
            {
                Place = p,
                ImageIds = p.Images.Select(i => i.Id)
            });

        var queryResult = await queryWithImageIds.ToListAsync();

        return [.. queryResult.Select(q =>
        {
            q.Place.ImageIds = [.. q.ImageIds];
            return q.Place;
        })];
    }

    public async Task<List<Place>> GetByWishlistIdAsync(string wishlistId)
    {
        // TODO: check for user access to the wishlist
        var query = _context.Places
            .AsNoTracking()
            .Where(p => p.WishlistId == wishlistId)
            .Select(p => new
            {
                Place = p,
                ImageIds = p.Images.Select(i => i.Id)
            });

        var queryResult = await query.ToListAsync();

        return [.. queryResult.Select(q =>
        {
            q.Place.ImageIds = [.. q.ImageIds];
            return q.Place;
        })];
    }

    public async Task<List<string>> GetAllTagsByUserAsync(string userId)
    {
        var userWishlistIds = GetUserWishlistIds(userId);
        var tripPlaceIds = GetOwnedTripPlaceIds(userId);

        var tags = await _context.Places
            .AsNoTracking()
            .Where(p => (p.WishlistId != null && userWishlistIds.Contains(p.WishlistId)) ||
                tripPlaceIds.Contains(p.Id))
            .Select(p => p.Tags)
            .ToListAsync();

        return [.. tags
            .SelectMany(t => t)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t)];
    }

}
