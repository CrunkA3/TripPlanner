using System.Security.Claims;
using System.Text;
using TripPlanner.Web.Models;
using TripPlanner.Web.Repositories;
using TripPlanner.Web.Services;

namespace TripPlanner.Web.API;

internal static class GpxDownloadApi
{
    internal static IEndpointConventionBuilder MapGpxDownloadApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/gpx")
            .WithDisplayName("GPX Download API");

        group.MapGet("/{trackId}", DownloadGpxAsync)
            .WithName("DownloadGpx")
            .WithDisplayName("Download GPX Track")
            .AllowAnonymous()
            .Produces(StatusCodes.Status200OK, contentType: "application/gpx+xml")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> DownloadGpxAsync(
        string trackId,
        string? token,
        ClaimsPrincipal user,
        IGpxRepository gpxRepository,
        GpxService gpxService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(trackId))
            return Results.BadRequest("Track ID cannot be empty.");

        GpxTrack? track;
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is not null)
        {
            // Authenticated: check ownership via user ID
            track = await gpxRepository.GetByIdWithPointsAsync(trackId, userId, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(token))
        {
            // Anonymous with a public share token: check collection membership
            track = await gpxRepository.GetByIdWithPointsByPublicTokenAsync(trackId, token, cancellationToken);
        }
        else
        {
            return Results.Unauthorized();
        }

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
