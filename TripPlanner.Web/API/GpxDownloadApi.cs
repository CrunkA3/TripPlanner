using System.Security.Claims;
using System.Text;
using TripPlanner.Web.Repositories;
using TripPlanner.Web.Services;

namespace TripPlanner.Web.API;

internal static class GpxDownloadApi
{
    internal static IEndpointConventionBuilder MapGpxDownloadApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/gpx")
            .WithDisplayName("GPX Download API")
            .RequireAuthorization();

        group.MapGet("/{trackId}", DownloadGpxAsync)
            .WithName("DownloadGpx")
            .WithDisplayName("Download GPX Track")
            .Produces(StatusCodes.Status200OK, contentType: "application/gpx+xml")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> DownloadGpxAsync(
        string trackId,
        ClaimsPrincipal user,
        IGpxRepository gpxRepository,
        GpxService gpxService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(trackId))
            return Results.BadRequest("Track ID cannot be empty.");

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Results.Unauthorized();

        var track = await gpxRepository.GetByIdWithPointsAsync(trackId, userId, cancellationToken);
        if (track is null)
            return Results.NotFound();

        var gpxContent = gpxService.SerializeToGpx(track);
        var fileName = SanitizeFileName(track.Name) + ".gpx";
        var bytes = Encoding.UTF8.GetBytes(gpxContent);

        return Results.File(bytes, "application/gpx+xml", fileName);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c)).Trim();
        return string.IsNullOrEmpty(sanitized) ? "track" : sanitized;
    }
}
