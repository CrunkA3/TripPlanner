using System.Text.Json;
using Microsoft.JSInterop;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public class LocalStorageGpxRepository : IGpxRepository
{
    private const string StorageKey = "tripplanner_gpxtracks";
    private readonly IJSRuntime _jsRuntime;

    public LocalStorageGpxRepository(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<List<GpxTrack>> GetAllAsync()
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (string.IsNullOrEmpty(json))
                return new List<GpxTrack>();
            
            return JsonSerializer.Deserialize<List<GpxTrack>>(json) ?? new List<GpxTrack>();
        }
        catch
        {
            return new List<GpxTrack>();
        }
    }

    public async Task<GpxTrack?> GetByIdAsync(string id)
    {
        var tracks = await GetAllAsync();
        return tracks.FirstOrDefault(t => t.Id == id);
    }

    public async Task<GpxTrack> AddAsync(GpxTrack track)
    {
        var tracks = await GetAllAsync();
        track.CreatedAt = DateTime.UtcNow;
        tracks.Add(track);
        await SaveAllAsync(tracks);
        return track;
    }

    public async Task DeleteAsync(string id)
    {
        var tracks = await GetAllAsync();
        tracks.RemoveAll(t => t.Id == id);
        await SaveAllAsync(tracks);
    }

    private async Task SaveAllAsync(List<GpxTrack> tracks)
    {
        var json = JsonSerializer.Serialize(tracks);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }
}
