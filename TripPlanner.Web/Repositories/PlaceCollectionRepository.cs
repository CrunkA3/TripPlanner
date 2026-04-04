using Microsoft.EntityFrameworkCore;
using TripPlanner.Web.Data;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public class PlaceCollectionRepository : IPlaceCollectionRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public PlaceCollectionRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<PlaceCollection>> GetAllByOwnerAsync(string userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var collectionsWithCounts = await context.PlaceCollections
            .AsNoTracking()
            .Where(c => c.OwnerId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                Collection = c,
                ItemCount = c.Items.Count()
            })
            .ToListAsync();

        foreach (var entry in collectionsWithCounts)
        {
            entry.Collection.Items = Enumerable.Range(0, entry.ItemCount)
                .Select(_ => new PlaceCollectionItem())
                .ToList();
        }

        return collectionsWithCounts
            .Select(entry => entry.Collection)
            .ToList();
    }

    public async Task<PlaceCollection?> GetByIdAsync(string id)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.PlaceCollections
            .AsNoTracking()
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<PlaceCollection?> GetByPublicTokenAsync(string token)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.PlaceCollections
            .AsNoTracking()
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.PublicShareToken == token);
    }

    public async Task<PlaceCollection> AddAsync(PlaceCollection collection)
    {
        await using var context = _contextFactory.CreateDbContext();
        context.PlaceCollections.Add(collection);
        await context.SaveChangesAsync();
        return collection;
    }

    public async Task<PlaceCollection> UpdateAsync(PlaceCollection collection)
    {
        await using var context = _contextFactory.CreateDbContext();
        var existing = await context.PlaceCollections
            .FirstOrDefaultAsync(c => c.Id == collection.Id && c.OwnerId == collection.OwnerId);
        if (existing == null) return collection;

        existing.Name = collection.Name;
        existing.Description = collection.Description;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(string id, string userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var collection = await context.PlaceCollections
            .FirstOrDefaultAsync(c => c.Id == id && c.OwnerId == userId);
        if (collection != null)
        {
            context.PlaceCollections.Remove(collection);
            await context.SaveChangesAsync();
        }
    }

    public async Task AddPlaceAsync(string collectionId, string placeId, string userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var collectionExists = await context.PlaceCollections
            .AnyAsync(c => c.Id == collectionId && c.OwnerId == userId);
        if (!collectionExists) return;

        var alreadyAdded = await context.PlaceCollectionItems
            .AnyAsync(i => i.CollectionId == collectionId && i.PlaceId == placeId);
        if (alreadyAdded) return;

        context.PlaceCollectionItems.Add(new PlaceCollectionItem
        {
            CollectionId = collectionId,
            PlaceId = placeId
        });
        await context.SaveChangesAsync();
    }

    public async Task RemovePlaceAsync(string collectionId, string placeId, string userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var collectionExists = await context.PlaceCollections
            .AnyAsync(c => c.Id == collectionId && c.OwnerId == userId);
        if (!collectionExists) return;

        var item = await context.PlaceCollectionItems
            .FirstOrDefaultAsync(i => i.CollectionId == collectionId && i.PlaceId == placeId);
        if (item != null)
        {
            context.PlaceCollectionItems.Remove(item);
            await context.SaveChangesAsync();
        }
    }

    public async Task<string?> GeneratePublicLinkAsync(string collectionId, string userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var collection = await context.PlaceCollections
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.OwnerId == userId);
        if (collection == null) return null;

        collection.PublicShareToken = Guid.NewGuid().ToString("N");
        collection.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();
        return collection.PublicShareToken;
    }

    public async Task RevokePublicLinkAsync(string collectionId, string userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var collection = await context.PlaceCollections
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.OwnerId == userId);
        if (collection != null)
        {
            collection.PublicShareToken = null;
            collection.UpdatedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    public async Task<List<Place>> GetPlacesAsync(string collectionId, string userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var collectionExists = await context.PlaceCollections
            .AsNoTracking()
            .AnyAsync(c => c.Id == collectionId && c.OwnerId == userId);
        if (!collectionExists) return [];

        return await LoadPlacesForCollection(context, collectionId);
    }

    public async Task<List<Place>> GetPlacesByPublicTokenAsync(string token)
    {
        await using var context = _contextFactory.CreateDbContext();
        var collection = await context.PlaceCollections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.PublicShareToken == token);
        if (collection == null) return [];

        return await LoadPlacesForCollection(context, collection.Id);
    }

    private static async Task<List<Place>> LoadPlacesForCollection(ApplicationDbContext context, string collectionId)
    {
        var query = context.PlaceCollectionItems
            .AsNoTracking()
            .Where(i => i.CollectionId == collectionId)
            .OrderBy(i => i.AddedAt)
            .Select(i => new
            {
                Place = i.Place,
                GpxTrack = i.Place != null ? i.Place.GpxTrack : null,
                ImageIds = i.Place != null
                    ? context.PlaceImages
                        .Where(img => img.PlaceId == i.Place.Id)
                        .OrderBy(img => img.SortOrder)
                        .Select(img => img.Id)
                    : Enumerable.Empty<string>()
            });

        var results = await query.ToListAsync();

        return [.. results
            .Where(r => r.Place != null)
            .Select(r =>
            {
                r.Place!.ImageIds = [.. r.ImageIds];
                r.Place!.GpxTrack = r.GpxTrack;
                return r.Place;
            })];
    }
}
