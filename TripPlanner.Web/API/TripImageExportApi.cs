using System.Security.Claims;
using TripPlanner.Web.Repositories;
using TripPlanner.Web.Services;

namespace TripPlanner.Web.API;

internal static class TripImageExportApi
{
    internal static IEndpointConventionBuilder MapTripImageExportApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/trips")
            .WithDisplayName("Trip Image Export API");

        group.MapGet("/{tripId}/export-image", ExportTripImageAsync)
            .WithName("ExportTripImage")
            .WithDisplayName("Export Trip Image")
            .Produces(StatusCodes.Status200OK, contentType: "image/png")
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> ExportTripImageAsync(
        string tripId,
        ClaimsPrincipal user,
        ITripRepository tripRepository,
        TripMapExportService tripMapExportService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tripId))
        {
            return Results.BadRequest("Trip ID cannot be empty.");
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Unauthorized();
        }

        var hasAccess = await tripRepository.CanUserAccessAsync(tripId, userId);
        if (!hasAccess)
        {
            return Results.NotFound();
        }

        var trip = await tripRepository.GetByIdAsync(tripId);
        if (trip is null)
        {
            return Results.NotFound();
        }

        try
        {
            var imageBytes = await tripMapExportService.RenderTripAsync(trip, cancellationToken);
            return Results.File(imageBytes, "image/png", $"{SanitizeFileName(trip.Name)}-map.png");
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Concat(name.Select(character => invalidChars.Contains(character) ? '_' : character)).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "trip" : sanitized;
    }
}
