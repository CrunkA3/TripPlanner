using System.ComponentModel;
using System.Security.Claims;
using System.Text.Json;
using ModelContextProtocol.Server;
using TripPlanner.Web.Models;
using TripPlanner.Web.Repositories;

namespace TripPlanner.Web.McpTools;

[McpServerToolType]
public class TripMcpTools(ITripRepository tripRepository, IHttpContextAccessor httpContextAccessor)
{
    private string? UserId => httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    [McpServerTool, Description("List all trips owned by or shared with the current user.")]
    public async Task<string> ListTrips()
    {
        if (UserId is null) return "Unauthorized.";

        var owned = await tripRepository.GetByOwnerAsync(UserId);
        var shared = await tripRepository.GetSharedWithUserAsync(UserId);
        var all = owned.Concat(shared).DistinctBy(t => t.Id);

        var result = all.Select(t => new
        {
            t.Id,
            t.Name,
            t.Description,
            StartDate = t.StartDate?.ToString("yyyy-MM-dd"),
            EndDate = t.EndDate?.ToString("yyyy-MM-dd"),
            DayCount = t.Days.Count,
            IsOwner = t.OwnerId == UserId
        });

        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Get the full details of a trip including all days and scheduled places.")]
    public async Task<string> GetTrip([Description("The trip ID.")] string tripId)
    {
        if (UserId is null) return "Unauthorized.";

        if (!await tripRepository.CanUserAccessAsync(tripId, UserId))
            return "Trip not found or access denied.";

        var trip = await tripRepository.GetByIdAsync(tripId);
        if (trip is null) return "Trip not found.";

        var result = new
        {
            trip.Id,
            trip.Name,
            trip.Description,
            StartDate = trip.StartDate?.ToString("yyyy-MM-dd"),
            EndDate = trip.EndDate?.ToString("yyyy-MM-dd"),
            Days = trip.Days.OrderBy(d => d.DayNumber).Select(d => new
            {
                d.Id,
                d.DayNumber,
                Date = d.Date?.ToString("yyyy-MM-dd"),
                Places = d.Places.OrderBy(p => p.Order).Select(p => new
                {
                    TripPlaceId = p.Id,
                    p.PlaceId,
                    PlaceName = p.Place?.Name,
                    ScheduledTime = p.ScheduledTime?.ToString("HH:mm"),
                    p.DurationMinutes,
                    p.Notes,
                    p.Order
                })
            }),
            UnscheduledPlaces = trip.UnscheduledPlaces.Select(p => new
            {
                TripPlaceId = p.Id,
                p.PlaceId,
                PlaceName = p.Place?.Name
            }),
            Accommodations = trip.Accommodations.Select(a => new
            {
                a.Id,
                a.Name,
                a.Address,
                CheckIn = a.PlannedCheckIn?.ToString("yyyy-MM-dd"),
                CheckOut = a.PlannedCheckOut?.ToString("yyyy-MM-dd")
            })
        };

        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Create a new trip.")]
    public async Task<string> CreateTrip(
        [Description("The name of the trip.")] string name,
        [Description("An optional description.")] string? description = null,
        [Description("Optional start date in ISO 8601 format (yyyy-MM-dd).")] string? startDate = null,
        [Description("Optional end date in ISO 8601 format (yyyy-MM-dd).")] string? endDate = null)
    {
        if (UserId is null) return "Unauthorized.";

        var trip = new Trip
        {
            Name = name,
            Description = description ?? string.Empty,
            OwnerId = UserId,
            StartDate = startDate is not null ? DateTime.TryParse(startDate, out var sd) ? sd : null : null,
            EndDate = endDate is not null ? DateTime.TryParse(endDate, out var ed) ? ed : null : null
        };

        var created = await tripRepository.AddAsync(trip);
        return JsonSerializer.Serialize(new { created.Id, created.Name, Message = "Trip created successfully." });
    }

    [McpServerTool, Description("Update an existing trip's metadata (name, description, dates).")]
    public async Task<string> UpdateTrip(
        [Description("The ID of the trip to update.")] string tripId,
        [Description("New name (optional).")] string? name = null,
        [Description("New description (optional).")] string? description = null,
        [Description("New start date in ISO 8601 format yyyy-MM-dd (optional).")] string? startDate = null,
        [Description("New end date in ISO 8601 format yyyy-MM-dd (optional).")] string? endDate = null)
    {
        if (UserId is null) return "Unauthorized.";

        var trip = await tripRepository.GetByIdAsync(tripId);
        if (trip is null || trip.OwnerId != UserId)
            return "Trip not found or access denied.";

        if (name is not null) trip.Name = name;
        if (description is not null) trip.Description = description;
        if (startDate is not null) trip.StartDate = DateTime.TryParse(startDate, out var sd) ? sd : trip.StartDate;
        if (endDate is not null) trip.EndDate = DateTime.TryParse(endDate, out var ed) ? ed : trip.EndDate;

        await tripRepository.UpdateAsync(trip);
        return "Trip updated successfully.";
    }

    [McpServerTool, Description("Delete a trip and all its contents.")]
    public async Task<string> DeleteTrip([Description("The ID of the trip to delete.")] string tripId)
    {
        if (UserId is null) return "Unauthorized.";

        var trip = await tripRepository.GetByIdAsync(tripId);
        if (trip is null || trip.OwnerId != UserId)
            return "Trip not found or access denied.";

        await tripRepository.DeleteAsync(tripId);
        return "Trip deleted successfully.";
    }
}
