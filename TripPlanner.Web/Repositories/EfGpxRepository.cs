using Microsoft.EntityFrameworkCore;
using TripPlanner.Web.Data;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public class EfGpxRepository : IGpxRepository
{
    private readonly ApplicationDbContext _context;

    public EfGpxRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<GpxTrack>> GetAllAsync()
    {
        return await _context.GpxTracks.ToListAsync();
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

    public async Task DeleteAsync(string id)
    {
        var track = await _context.GpxTracks.FindAsync(id);
        if (track != null)
        {
            _context.GpxTracks.Remove(track);
            await _context.SaveChangesAsync();
        }
    }
}
