using System.Text.Json;
using Microsoft.JSInterop;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public class LocalStorageTripRepository : ITripRepository
{
    private const string StorageKey = "tripplanner_trips";
    private readonly IJSRuntime _jsRuntime;

    public LocalStorageTripRepository(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<List<Trip>> GetAllAsync()
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (string.IsNullOrEmpty(json))
                return new List<Trip>();
            
            return JsonSerializer.Deserialize<List<Trip>>(json) ?? new List<Trip>();
        }
        catch
        {
            return new List<Trip>();
        }
    }

    public async Task<Trip?> GetByIdAsync(string id)
    {
        var trips = await GetAllAsync();
        return trips.FirstOrDefault(t => t.Id == id);
    }

    public async Task<Trip> AddAsync(Trip trip)
    {
        var trips = await GetAllAsync();
        trip.CreatedAt = DateTime.UtcNow;
        trips.Add(trip);
        await SaveAllAsync(trips);
        return trip;
    }

    public async Task<Trip> UpdateAsync(Trip trip)
    {
        var trips = await GetAllAsync();
        var index = trips.FindIndex(t => t.Id == trip.Id);
        if (index >= 0)
        {
            trip.UpdatedAt = DateTime.UtcNow;
            trips[index] = trip;
            await SaveAllAsync(trips);
        }
        return trip;
    }

    public async Task DeleteAsync(string id)
    {
        var trips = await GetAllAsync();
        trips.RemoveAll(t => t.Id == id);
        await SaveAllAsync(trips);
    }

    private async Task SaveAllAsync(List<Trip> trips)
    {
        var json = JsonSerializer.Serialize(trips);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }
}
