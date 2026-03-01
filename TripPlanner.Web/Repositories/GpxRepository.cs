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
        return await _context.GpxTracks.Include(t => t.Points.OrderBy(x => x.Order)).ToListAsync();
    }


    public async Task<List<GpxTrack>> GetByTripIdAsync(string tripId)
    {
        var trackIds = _context.Trips.Where(t => t.Id == tripId)
            .Include(t => t.Days).ThenInclude(d => d.Places).ThenInclude(p => p.Place)
            .SelectMany(t => t.Days.SelectMany(d => d.Places.Select(p => p.Place)))
            .Where(p => p != null && !string.IsNullOrEmpty(p.GpxTrackId))
            .Select(p => p!.GpxTrackId);

        return await _context.GpxTracks
            .Where(x => trackIds.Any(trackId => x.Id == trackId))
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
