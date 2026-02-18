using Microsoft.EntityFrameworkCore;
using TripPlanner.Web.Data;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public class WishlistRepository : IWishlistRepository
{
    private readonly ApplicationDbContext _context;

    public WishlistRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Wishlist>> GetAllByUserAsync(string userId)
    {
        return await _context.Wishlists
            .Where(w => w.OwnerId == userId)
            .Include(w => w.Places)
            .Include(w => w.SharedWith)
            .ToListAsync();
    }

    public async Task<List<Wishlist>> GetSharedWithUserAsync(string userId)
    {
        return await _context.UserWishlists
            .Where(uw => uw.UserId == userId)
            .Include(uw => uw.Wishlist)
                .ThenInclude(w => w!.Places)
            .Select(uw => uw.Wishlist!)
            .ToListAsync();
    }

    public async Task<Wishlist?> GetByIdAsync(string id)
    {
        return await _context.Wishlists
            .Include(w => w.Places)
            .Include(w => w.SharedWith)
            .FirstOrDefaultAsync(w => w.Id == id);
    }

    public async Task<Wishlist> AddAsync(Wishlist wishlist)
    {
        _context.Wishlists.Add(wishlist);
        await _context.SaveChangesAsync();
        return wishlist;
    }

    public async Task<Wishlist> UpdateAsync(Wishlist wishlist)
    {
        wishlist.UpdatedAt = DateTime.UtcNow;
        _context.Wishlists.Update(wishlist);
        await _context.SaveChangesAsync();
        return wishlist;
    }

    public async Task DeleteAsync(string id)
    {
        var wishlist = await _context.Wishlists.FindAsync(id);
        if (wishlist != null)
        {
            _context.Wishlists.Remove(wishlist);
            await _context.SaveChangesAsync();
        }
    }

    public async Task ShareWithUserAsync(string wishlistId, string userId)
    {
        var existing = await _context.UserWishlists
            .FirstOrDefaultAsync(uw => uw.WishlistId == wishlistId && uw.UserId == userId);

        if (existing == null)
        {
            _context.UserWishlists.Add(new UserWishlist
            {
                WishlistId = wishlistId,
                UserId = userId
            });
            await _context.SaveChangesAsync();
        }
    }

    public async Task UnshareWithUserAsync(string wishlistId, string userId)
    {
        var userWishlist = await _context.UserWishlists
            .FirstOrDefaultAsync(uw => uw.WishlistId == wishlistId && uw.UserId == userId);

        if (userWishlist != null)
        {
            _context.UserWishlists.Remove(userWishlist);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> CanUserAccessAsync(string wishlistId, string userId)
    {
        return await _context.Wishlists
            .AnyAsync(w => w.Id == wishlistId && w.OwnerId == userId)
            || await _context.UserWishlists
                .AnyAsync(uw => uw.WishlistId == wishlistId && uw.UserId == userId);
    }
}
