using Microsoft.EntityFrameworkCore;
using TripPlanner.Web.Data;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public class PlaceRepository : IPlaceRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public PlaceRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private static IQueryable<string> GetUserWishlistIds(ApplicationDbContext context, string userId) =>
        context.UserWishlists
            .Where(w => w.UserId == userId)
            .Select(w => w.WishlistId);

    private static IQueryable<string> GetUserTripIds(ApplicationDbContext context, string userId) =>
        context.Trips
            .Where(t => t.OwnerId == userId)
            .Select(t => t.Id)
            .Union(context.SharedTrips
                .Where(st => st.UserId == userId)
                .Select(st => st.TripId));

    private static IQueryable<string> GetOwnedTripPlaceIds(ApplicationDbContext context, string userId) =>
        context.Trips
            .Where(t => t.OwnerId == userId)
            .SelectMany(t => t.Days.SelectMany(d => d.Places.Select(tp => tp.PlaceId)));

    /// <summary>Filters a places query to only places the user can access (via wishlist or trip membership).</summary>
    private static IQueryable<Place> WithUserAccess(
        IQueryable<Place> query,
        IQueryable<string> userWishlistIds,
        IQueryable<string> userTripIds) =>
        query.Where(p =>
            (p.WishlistId != null && userWishlistIds.Contains(p.WishlistId)) ||
            (p.TripId != null && userTripIds.Contains(p.TripId)));

    public async Task<List<Place>> GetAllByUserAsync(string userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var userWishlistIds = GetUserWishlistIds(context, userId);
        var tripPlaceIds = GetOwnedTripPlaceIds(context, userId);

        var query = context.Places
            .AsNoTracking()
            .Where(p => p.Wishlist != null && (
                            userWishlistIds.Contains(p.WishlistId!) ||
                            tripPlaceIds.Contains(p.Id)))
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Include(p => p.Wishlist)
            .Include(p => p.Trip)
            .Select(p => new
            {
                Place = p,
                ImageIds = p.Images.OrderBy(i => i.SortOrder).Select(i => i.Id),
                GpxTrack = p.GpxTrack
            });

        var queryResult = await query.ToListAsync();

        return [.. queryResult.Select(q =>
        {
            q.Place.ImageIds = [.. q.ImageIds];
            q.Place.GpxTrack = q.GpxTrack;
            return q.Place;
        })];
    }


    public async Task<List<Place>> GetAllWithAnyWishlistAsync(string userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var userWishlistIds = GetUserWishlistIds(context, userId);

        var query = context.Places
            .AsNoTracking()
            .Where(p => p.WishlistId != null && userWishlistIds.Contains(p.WishlistId))
            .OrderBy(p => p.Name)
            .Include(p => p.Wishlist)
            .ThenInclude(wl => wl!.SharedWith)
            .Select(p => new
            {
                Place = p,
                ImageIds = p.Images.OrderBy(i => i.SortOrder).Select(i => i.Id),
                GpxTrack = p.GpxTrack
            });

        var queryResult = await query.ToListAsync();

        return [.. queryResult.Select(q =>
        {
            q.Place.ImageIds = [.. q.ImageIds];
            q.Place.GpxTrack = q.GpxTrack;
            return q.Place;
        })];
    }

    public async Task<List<Place>> GetAllForTripAsync(string tripId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var query = context.Places
            .AsNoTracking()
            .Where(p => p.TripId == tripId)
            .OrderBy(p => p.Name)
            .Include(p => p.Wishlist)
            .Select(p => new
            {
                Place = p,
                ImageIds = p.Images.OrderBy(i => i.SortOrder).Select(i => i.Id),
                GpxTrack = p.GpxTrack
            }); 

        var queryResult = await query.ToListAsync();

        return [.. queryResult.Select(q =>
        {
            q.Place.ImageIds = [.. q.ImageIds];
            q.Place.GpxTrack = q.GpxTrack;
            return q.Place;
        })];
    }

    public async Task<Place?> GetByIdAsync(string id, string userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var userWishlistIds = GetUserWishlistIds(context, userId);
        var userTripIds = GetUserTripIds(context, userId);

        var queryResult = await WithUserAccess(context.Places.Where(p => p.Id == id), userWishlistIds, userTripIds)
            .AsNoTracking()
            .Include(p => p.Wishlist)
            .Include(p => p.Trip)
            .Include(p => p.Images)
            .Select(p => new
            {
                Place = p,
                ImageIds = p.Images.OrderBy(i => i.SortOrder).Select(i => i.Id)
            })
            .FirstOrDefaultAsync();

        queryResult?.Place.ImageIds = [.. queryResult?.ImageIds ?? []];

        return queryResult?.Place;
    }

    public async Task<bool> ExistsAsync(string id)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.Places.AnyAsync(p => p.Id == id);
    }

    public async Task<bool> HasAccessAsync(string id, string userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var userWishlistIds = GetUserWishlistIds(context, userId);
        var userTripIds = GetUserTripIds(context, userId);

        return await WithUserAccess(context.Places.Where(p => p.Id == id), userWishlistIds, userTripIds).AnyAsync();
    }

    public async Task<Place> AddAsync(Place place)
    {
        await using var context = _contextFactory.CreateDbContext();
        context.Places.Add(place);
        await context.SaveChangesAsync();

        place.ImageIds = [.. place.Images.OrderBy(i => i.SortOrder).Select(i => i.Id)];

        return place;
    }

    public async Task<Place> UpdateAsync(Place place)
    {
        await using var context = _contextFactory.CreateDbContext();
        place.UpdatedAt = DateTimeOffset.UtcNow;

        // Sync images: load existing, add new, update existing, remove deleted
        var existingImages = await context.PlaceImages
            .AsNoTracking()
            .Where(pi => pi.PlaceId == place.Id)
            .ToListAsync();

        var newImageIds = place.Images.OrderBy(i => i.SortOrder).Select(i => i.Id).ToHashSet();
        var toRemove = existingImages.Where(ei => !newImageIds.Contains(ei.Id)).ToList();
        context.PlaceImages.RemoveRange(toRemove);

        var existingImageIds = existingImages.Select(i => i.Id).ToHashSet();
        foreach (var img in place.Images)
        {
            img.PlaceId = place.Id;
            if (!existingImageIds.Contains(img.Id))
            {
                context.PlaceImages.Add(img);
            }
            else if (img.ImageData?.Length > 0)
            {
                // Only update the row if the dialog replaced the image blob.
                // Stub entries (ImageData is empty) represent unchanged DB images loaded
                // via the /api/placeImages endpoint and must not overwrite existing data.
                context.Entry(img).State = EntityState.Modified;
            }
        }

        context.Entry(place).State = EntityState.Modified;
        await context.SaveChangesAsync();
        return place;
    }

    public async Task<bool> MarkAsReviewedAsync(string id, string userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var userWishlistIds = GetUserWishlistIds(context, userId);
        var userTripIds = GetUserTripIds(context, userId);

        var updatedAt = DateTimeOffset.UtcNow;
        var count = await WithUserAccess(context.Places.Where(p => p.Id == id), userWishlistIds, userTripIds)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.NeedsReview, false)
                .SetProperty(p => p.UpdatedAt, updatedAt));

        return count > 0;
    }

    public async Task DeleteAsync(string id, string userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var userWishlistIds = GetUserWishlistIds(context, userId);
        var userTripIds = GetUserTripIds(context, userId);

        var place = await WithUserAccess(context.Places.Where(p => p.Id == id), userWishlistIds, userTripIds)
            .FirstOrDefaultAsync();
        if (place != null)
        {
            context.Places.Remove(place);
            await context.SaveChangesAsync();
        }
    }

    public async Task<List<Place>> FilterAsync(PlaceCategory? category = null, List<string>? tags = null, bool? hasGpxTrack = null)
    {
        await using var context = _contextFactory.CreateDbContext();
        var query = context.Places.AsNoTracking();

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
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Include(p => p.Wishlist)
            .Select(p => new
            {
                Place = p,
                ImageIds = p.Images.OrderBy(i => i.SortOrder).Select(i => i.Id),
                GpxTrack = p.GpxTrack
            });

        var queryResult = await queryWithImageIds.ToListAsync();

        return [.. queryResult.Select(q =>
        {
            q.Place.ImageIds = [.. q.ImageIds];
            q.Place.GpxTrack = q.GpxTrack;
            return q.Place;
        })];
    }

    public async Task<List<Place>> GetByWishlistIdAsync(string wishlistId, string userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var userWishlistIds = GetUserWishlistIds(context, userId);
        var hasAccess = await userWishlistIds.AnyAsync(id => id == wishlistId);
        if (!hasAccess)
            return [];

        var query = context.Places
            .AsNoTracking()
            .Where(p => p.WishlistId == wishlistId)
            .Include(p => p.Wishlist)
            .ThenInclude(wl => wl!.SharedWith)
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                Place = p,
                ImageIds = p.Images.OrderBy(i => i.SortOrder).Select(i => i.Id),
                GpxTrack = p.GpxTrack
            });

        var queryResult = await query.ToListAsync();

        return [.. queryResult.Select(q =>
        {
            q.Place.ImageIds = [.. q.ImageIds];
            q.Place.GpxTrack = q.GpxTrack;
            return q.Place;
        })];
    }

    public async Task<List<string>> GetAllTagsByUserAsync(string userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var userWishlistIds = GetUserWishlistIds(context, userId);
        var tripPlaceIds = GetOwnedTripPlaceIds(context, userId);

        var tags = await context.Places
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

    public async Task<PlaceImage?> GetPlaceImageAsync(string imageId, string userId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        var userWishlistIds = GetUserWishlistIds(context, userId);
        var userTripIds = GetUserTripIds(context, userId);

        var query = context.PlaceImages
            .AsNoTracking()
            .Include(pi => pi.Place)
            .Where(pi => pi.Id == imageId && (
                (pi.Place!.WishlistId != null && userWishlistIds.Contains(pi.Place.WishlistId)) ||
                (pi.Place.TripId != null && userTripIds.Contains(pi.Place.TripId))));
        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PlaceImage?> GetPlaceImageByPublicTokenAsync(string imageId, string publicToken, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();

        return await context.PlaceImages
            .AsNoTracking()
            .Where(pi => pi.Id == imageId &&
                context.PlaceCollectionItems.Any(i =>
                    i.PlaceId == pi.PlaceId &&
                    context.PlaceCollections.Any(c => c.Id == i.CollectionId && c.PublicShareToken == publicToken)))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
