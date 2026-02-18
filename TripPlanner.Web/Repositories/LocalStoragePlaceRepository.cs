using System.Text.Json;
using Microsoft.JSInterop;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public class LocalStoragePlaceRepository : IPlaceRepository
{
    private const string StorageKey = "tripplanner_places";
    private readonly IJSRuntime _jsRuntime;

    public LocalStoragePlaceRepository(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<List<Place>> GetAllAsync()
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (string.IsNullOrEmpty(json))
                return new List<Place>();
            
            return JsonSerializer.Deserialize<List<Place>>(json) ?? new List<Place>();
        }
        catch
        {
            return new List<Place>();
        }
    }

    public async Task<Place?> GetByIdAsync(string id)
    {
        var places = await GetAllAsync();
        return places.FirstOrDefault(p => p.Id == id);
    }

    public async Task<Place> AddAsync(Place place)
    {
        var places = await GetAllAsync();
        place.CreatedAt = DateTime.UtcNow;
        places.Add(place);
        await SaveAllAsync(places);
        return place;
    }

    public async Task<Place> UpdateAsync(Place place)
    {
        var places = await GetAllAsync();
        var index = places.FindIndex(p => p.Id == place.Id);
        if (index >= 0)
        {
            place.UpdatedAt = DateTime.UtcNow;
            places[index] = place;
            await SaveAllAsync(places);
        }
        return place;
    }

    public async Task DeleteAsync(string id)
    {
        var places = await GetAllAsync();
        places.RemoveAll(p => p.Id == id);
        await SaveAllAsync(places);
    }

    public async Task<List<Place>> FilterAsync(PlaceCategory? category = null, List<string>? tags = null, bool? hasGpxTrack = null)
    {
        var places = await GetAllAsync();
        
        if (category.HasValue)
            places = places.Where(p => p.Category == category.Value).ToList();
        
        if (tags != null && tags.Any())
            places = places.Where(p => p.Tags.Any(t => tags.Contains(t))).ToList();
        
        if (hasGpxTrack.HasValue)
            places = places.Where(p => p.HasGpxTrack == hasGpxTrack.Value).ToList();
        
        return places;
    }

    private async Task SaveAllAsync(List<Place> places)
    {
        var json = JsonSerializer.Serialize(places);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }
}
