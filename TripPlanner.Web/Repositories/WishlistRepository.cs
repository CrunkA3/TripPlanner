using Microsoft.EntityFrameworkCore;
using TripPlanner.Web.Data;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public class WishlistRepository : IWishlistRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public WishlistRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<Wishlist>> GetAllByUserAsync(string userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.Wishlists
            .AsNoTracking()
            .Where(ul => ul.SharedWith.Any(sw => sw.UserId == userId))
            .Include(w => w.Places)
            .Include(w => w.SharedWith)
            .ToListAsync();
    }


    public async Task<Wishlist?> GetByIdAsync(string id)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.Wishlists
            .AsNoTracking()
            .Include(w => w.Places)
            .Include(w => w.SharedWith)
            .FirstOrDefaultAsync(w => w.Id == id);
    }

    public async Task<Wishlist> AddAsync(Wishlist wishlist)
    {
        await using var context = _contextFactory.CreateDbContext();
        context.Wishlists.Add(wishlist);
        await context.SaveChangesAsync();
        return wishlist;
    }

    public async Task<Wishlist> UpdateAsync(Wishlist wishlist)
    {
        await using var context = _contextFactory.CreateDbContext();
        wishlist.UpdatedAt = DateTimeOffset.UtcNow;
        context.Wishlists.Update(wishlist);
        await context.SaveChangesAsync();
        return wishlist;
    }

    public async Task DeleteAsync(string id, string userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var wishlist = await context.Wishlists
            .FirstOrDefaultAsync(w => w.Id == id && w.SharedWith.Any(uw => uw.UserId == userId && uw.Level == ShareLevel.Owner));
        if (wishlist != null)
        {
            context.Wishlists.Remove(wishlist);
            await context.SaveChangesAsync();
        }
    }

    public async Task ShareWithUserAsync(string wishlistId, string userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var existing = await context.UserWishlists
            .FirstOrDefaultAsync(uw => uw.WishlistId == wishlistId && uw.UserId == userId);

        if (existing == null)
        {
            context.UserWishlists.Add(new UserWishlist
            {
                WishlistId = wishlistId,
                UserId = userId
            });
            await context.SaveChangesAsync();
        }
    }

    public async Task UnshareWithUserAsync(string wishlistId, string userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var userWishlist = await context.UserWishlists
            .FirstOrDefaultAsync(uw => uw.WishlistId == wishlistId && uw.UserId == userId);

        if (userWishlist != null)
        {
            context.UserWishlists.Remove(userWishlist);
            await context.SaveChangesAsync();
        }
    }

    public async Task<bool> CanUserAccessAsync(string wishlistId, string userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.UserWishlists
                .AsNoTracking()
                .AnyAsync(uw => uw.WishlistId == wishlistId && uw.UserId == userId);
    }

    public async Task<bool> CanUserAdministrateAsync(string wishlistId, string userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.UserWishlists
                .AsNoTracking()
                .AnyAsync(uw => uw.WishlistId == wishlistId && uw.UserId == userId && uw.Level == ShareLevel.Owner);
    }

    public async Task<bool> CanUserEditAsync(string wishlistId, string userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.UserWishlists
                .AsNoTracking()
                .AnyAsync(uw => uw.WishlistId == wishlistId && uw.UserId == userId && uw.Level <= ShareLevel.Editor);
    }
}
