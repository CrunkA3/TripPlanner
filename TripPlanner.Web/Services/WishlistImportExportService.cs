using Microsoft.EntityFrameworkCore;
using System.Transactions;
using TripPlanner.Web.API;
using TripPlanner.Web.Data;
using TripPlanner.Web.Models;
using TripPlanner.Web.Repositories;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TripPlanner.Web.Services;

/// <summary>
/// DTO for a wishlist place image, used in YAML export/import.
/// </summary>
public sealed class PlaceImageExportDto
{
    [YamlMember(Alias = "content_type")]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Base64-encoded image data.</summary>
    [YamlMember(Alias = "data")]
    public string Data { get; set; } = string.Empty;
}

/// <summary>
/// DTO for a GPX track, represented as a polyline (list of [lat, lon] pairs) in the YAML export.
/// </summary>
public sealed class GpxTrackExportDto
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "description")]
    public string? Description { get; set; }

    [YamlMember(Alias = "total_distance_km")]
    public double TotalDistanceKm { get; set; }

    [YamlMember(Alias = "elevation_gain_m")]
    public double ElevationGainM { get; set; }

    [YamlMember(Alias = "elevation_loss_m")]
    public double ElevationLossM { get; set; }

    /// <summary>
    /// Ordered list of track points as [latitude, longitude] pairs.
    /// Elevation data is intentionally omitted for compactness.
    /// </summary>
    [YamlMember(Alias = "polyline")]
    public List<List<double>> Polyline { get; set; } = [];

    [YamlMember(Alias = "waypoints")]
    public List<GpxWaypointExportDto> Waypoints { get; set; } = [];
}

public sealed class GpxWaypointExportDto
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "latitude")]
    public double Latitude { get; set; }

    [YamlMember(Alias = "longitude")]
    public double Longitude { get; set; }
}

/// <summary>DTO for a single place, used in YAML export/import.</summary>
public sealed class PlaceExportDto
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "description")]
    public string? Description { get; set; }

    [YamlMember(Alias = "category")]
    public string Category { get; set; } = string.Empty;

    [YamlMember(Alias = "latitude")]
    public double Latitude { get; set; }

    [YamlMember(Alias = "longitude")]
    public double Longitude { get; set; }

    [YamlMember(Alias = "tags")]
    public List<string> Tags { get; set; } = [];

    [YamlMember(Alias = "notes")]
    public string? Notes { get; set; }

    [YamlMember(Alias = "images")]
    public List<PlaceImageExportDto> Images { get; set; } = [];

    [YamlMember(Alias = "gpx_track")]
    public GpxTrackExportDto? GpxTrack { get; set; }
}

/// <summary>Top-level DTO for a wishlist export.</summary>
public sealed class WishlistExportDto
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "description")]
    public string? Description { get; set; }

    [YamlMember(Alias = "exported_at")]
    public string ExportedAt { get; set; } = string.Empty;

    [YamlMember(Alias = "places")]
    public List<PlaceExportDto> Places { get; set; } = [];
}

/// <summary>
/// Service for exporting a wishlist to YAML and importing places from a YAML file.
/// </summary>
public partial class WishlistImportExportService(
    IWishlistRepository wishlistRepository,
    IPlaceRepository placeRepository,
    IGpxRepository gpxRepository,
    ILogger<WishlistImportExportService> logger,
    IDbContextFactory<ApplicationDbContext> contextFactory)
{
    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Exports a wishlist and all its places (with images as base64 and GPX tracks as polylines) to a YAML string.
    /// </summary>
    public async Task<string> ExportToYamlAsync(string wishlistId, string userId)
    {
        var wishlist = await wishlistRepository.GetByIdAsync(wishlistId)
            ?? throw new InvalidOperationException("Wishlist not found.");

        if (!wishlist.SharedWith.Any(uw => uw.UserId == userId))
            throw new UnauthorizedAccessException("Access denied.");

        var places = await placeRepository.GetByWishlistIdAsync(wishlistId, userId);

        // Load all images and GPX tracks for the wishlist in bulk to avoid N+1 queries
        var placeIds = places.Select(p => p.Id).ToList();
        await using var context = contextFactory.CreateDbContext();
        var allImages = await context.PlaceImages
            .AsNoTracking()
            .Where(img => placeIds.Contains(img.PlaceId))
            .OrderBy(img => img.SortOrder)
            .ToListAsync();
        var imagesByPlaceId = allImages
            .GroupBy(img => img.PlaceId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var gpxTrackIds = places
            .Where(p => p.GpxTrackId is not null)
            .Select(p => p.GpxTrackId!)
            .Distinct()
            .ToList();
        var gpxTracks = gpxTrackIds.Count > 0
            ? await context.GpxTracks
                .AsNoTracking()
                .Include(t => t.Points.OrderBy(p => p.Order))
                .Where(t => gpxTrackIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id)
            : [];

        var placeExportDtos = new List<PlaceExportDto>();
        foreach (var place in places)
        {
            var images = BuildImageDtos(imagesByPlaceId.GetValueOrDefault(place.Id) ?? []);
            GpxTrackExportDto? gpxTrack = null;
            if (place.GpxTrackId is not null && gpxTracks.TryGetValue(place.GpxTrackId, out var track))
                gpxTrack = ToGpxTrackExportDto(track);

            placeExportDtos.Add(new PlaceExportDto
            {
                Name = place.Name,
                Description = string.IsNullOrEmpty(place.Description) ? null : place.Description,
                Category = place.Category.ToString(),
                Latitude = place.Latitude,
                Longitude = place.Longitude,
                Tags = place.Tags ?? [],
                Notes = string.IsNullOrEmpty(place.Notes) ? null : place.Notes,
                Images = images,
                GpxTrack = gpxTrack
            });
        }

        var dto = new WishlistExportDto
        {
            Name = wishlist.Name,
            Description = string.IsNullOrEmpty(wishlist.Description) ? null : wishlist.Description,
            ExportedAt = DateTimeOffset.UtcNow.ToString("o"),
            Places = placeExportDtos
        };

        return YamlSerializer.Serialize(dto);
    }

    /// <summary>
    /// Imports places from a YAML string into an existing wishlist.
    /// Returns the number of places imported.
    /// </summary>
    public async Task<int> ImportFromYamlAsync(string yaml, string wishlistId, string userId)
    {
        var canEdit = await wishlistRepository.CanUserEditAsync(wishlistId, userId);
        if (!canEdit)
            throw new UnauthorizedAccessException("You do not have permission to edit this wishlist.");

        WishlistExportDto dto;
        try
        {
            dto = YamlDeserializer.Deserialize<WishlistExportDto>(yaml);
        }
        catch (Exception ex)
        {
            throw new FormatException($"Invalid YAML format: {ex.Message}", ex);
        }

        if (dto?.Places is null)
            return 0;

        var imported = 0;
        foreach (var placeDto in dto.Places)
        {
            if (string.IsNullOrWhiteSpace(placeDto.Name))
                continue;

            var place = new Place
            {
                Name = placeDto.Name,
                Description = placeDto.Description ?? string.Empty,
                Category = ParseCategory(placeDto.Category),
                Latitude = placeDto.Latitude,
                Longitude = placeDto.Longitude,
                Tags = placeDto.Tags ?? [],
                Notes = placeDto.Notes,
                WishlistId = wishlistId,
            };

            // Import images
            if (placeDto.Images is { Count: > 0 })
            {
                var sortOrder = 0;
                foreach (var imgDto in placeDto.Images)
                {
                    if (string.IsNullOrWhiteSpace(imgDto.Data)) continue;
                    // Do not serve potentially unsafe image types (e.g., SVG); treat as a bad request.
                    try
                    {
                        var safeContentType = PlaceImageApi.NormalizeAllowedImageContentType(imgDto.ContentType) ?? throw new ArgumentException("Unsupported image content type.");
                        var imageData = Convert.FromBase64String(imgDto.Data);
                        place.Images.Add(new PlaceImage
                        {
                            PlaceId = place.Id,
                            ImageData = imageData,
                            ImageContentType = string.IsNullOrEmpty(imgDto.ContentType) ? "image/jpeg" : imgDto.ContentType,
                            SortOrder = sortOrder++
                        });
                    }
                    catch (ArgumentException)
                    {
                        // Skip unsupported image types
                        LogSkippingUnsupportedImageContentType(imgDto.ContentType);
                    }
                    catch (FormatException)
                    {
                        // Skip malformed image data
                    }
                }
            }

            using var scope = new TransactionScope(
                TransactionScopeOption.Required,
                TransactionScopeAsyncFlowOption.Enabled);

            // Import GPX track as polyline
            if (placeDto.GpxTrack is { Polyline.Count: > 0 })
            {
                // Ensure there are at least two valid coordinate points before creating a track
                var validPointCount = 0;
                foreach (var point in placeDto.GpxTrack.Polyline)
                {
                    if (point.Count >= 2)
                    {
                        validPointCount++;
                        if (validPointCount >= 2)
                        {
                            break;
                        }
                    }
                }

                if (validPointCount >= 2)
                {
                    var track = new GpxTrack
                    {
                        Name = placeDto.GpxTrack.Name,
                        Description = placeDto.GpxTrack.Description,
                        TotalDistance = placeDto.GpxTrack.TotalDistanceKm,
                        ElevationGain = placeDto.GpxTrack.ElevationGainM,
                        ElevationLoss = placeDto.GpxTrack.ElevationLossM,
                        CreatedAt = DateTimeOffset.UtcNow
                    };

                    var order = 1;
                    foreach (var point in placeDto.GpxTrack.Polyline)
                    {
                        if (point.Count < 2) continue;
                        track.Points.Add(new GpxPoint
                        {
                            GpxTrackId = track.Id,
                            Latitude = point[0],
                            Longitude = point[1],
                            Order = order++
                        });
                    }

                    var waypointOrder = 1;
                    foreach (var waypoint in placeDto.GpxTrack.Waypoints ?? [])
                    {
                        track.Waypoints.Add(new GpxWaypoint
                        {
                            GpxTrackId = track.Id,
                            Name = waypoint.Name?.Trim() ?? string.Empty,
                            Latitude = waypoint.Latitude,
                            Longitude = waypoint.Longitude,
                            Order = waypointOrder++
                        });
                    }

                    var savedTrack = await gpxRepository.AddAsync(track);
                    place.GpxTrackId = savedTrack.Id;
                }
            }

            await placeRepository.AddAsync(place);
            scope.Complete();
            imported++;
        }

        return imported;
    }

    private static List<PlaceImageExportDto> BuildImageDtos(List<PlaceImage> images)
    {
        var result = new List<PlaceImageExportDto>();
        foreach (var img in images)
        {
            if (img.ImageData is { Length: > 0 })
            {
                result.Add(new PlaceImageExportDto
                {
                    ContentType = img.ImageContentType,
                    Data = Convert.ToBase64String(img.ImageData)
                });
            }
        }
        return result;
    }

    private static GpxTrackExportDto ToGpxTrackExportDto(GpxTrack track) => new()
    {
        Name = track.Name,
        Description = track.Description,
        TotalDistanceKm = track.TotalDistance,
        ElevationGainM = track.ElevationGain,
        ElevationLossM = track.ElevationLoss,
        Polyline = [.. track.Points
            .OrderBy(p => p.Order)
            .Select(p => new List<double> { p.Latitude, p.Longitude })],
        Waypoints = [.. track.Waypoints
            .OrderBy(w => w.Order)
            .Select(w => new GpxWaypointExportDto
            {
                Name = w.Name,
                Latitude = w.Latitude,
                Longitude = w.Longitude
            })]
    };

    private static PlaceCategory ParseCategory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return PlaceCategory.Other;
        return Enum.TryParse<PlaceCategory>(value, ignoreCase: true, out var parsed)
            ? parsed
            : PlaceCategory.Other;
    }


    [LoggerMessage(LogLevel.Debug, Message = "Skipping image with unsupported content type: {contentType}")]
    private partial void LogSkippingUnsupportedImageContentType(string contentType);

}
