using System.Security.Claims;
using System.Security.Cryptography;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using TripPlanner.Web.Repositories;

namespace TripPlanner.Web.API;

internal static class PlaceImageApi
{
    // Allowed resize widths to prevent abuse and limit resource usage.
    private static readonly int[] AllowedWidths = [400, 800, 1200];

    // Maximum cache lifetime for private image responses: 1 day (86400 seconds).
    private const int CacheMaxAgeSeconds = 86400;
    private const string CacheControlValue = "private, max-age=86400";

    internal static IEndpointConventionBuilder MapPlaceImageApi(this IEndpointRouteBuilder endpoints)
    {
        var groupPlaceImages = endpoints.MapGroup("/api/placeImages")
            .WithDisplayName("Place Image API")
            .RequireAuthorization();

        // Endpoint to retrieve a place image by its ID.
        // Optional 'width' query parameter resizes the image proportionally (allowed: 400, 800, 1200).
        groupPlaceImages.MapGet("/{imageId}", GetPlaceImageAsync)
            .WithName("GetPlaceImage")
            .WithDisplayName("Get Place Image")
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg", additionalContentTypes: "image/png")
            .Produces(StatusCodes.Status304NotModified)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);


        return groupPlaceImages;
    }


    private static async Task<IResult> GetPlaceImageAsync(string imageId, int? width, ClaimsPrincipal user, IPlaceRepository placeRepository, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageId))
        {
            return Results.BadRequest("Image ID cannot be null or empty.");
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        if (width is not null && !AllowedWidths.Contains(width.Value))
        {
            return Results.BadRequest($"Invalid width. Allowed values: {string.Join(", ", AllowedWidths)}.");
        }

        // Check if the image exists and belongs to the current user
        var placeImage = await placeRepository.GetPlaceImageAsync(imageId, userId, cancellationToken);
        if (placeImage is null)
        {
            return Results.NotFound();
        }

        var safeContentType = NormalizeAllowedImageContentType(placeImage.ImageContentType);
        if (safeContentType is null)
        {
            // Do not serve potentially unsafe image types (e.g., SVG); treat as a bad request.
            return Results.BadRequest("Unsupported image content type.");
        }

        // Compute an ETag from the raw stored data and the requested width so that
        // each unique (image, width) combination gets a stable, deterministic tag.
        var etag = ComputeETag(placeImage.ImageData, width);

        var imageData = placeImage.ImageData;
        var outputContentType = safeContentType;
        if (width is not null)
        {
            (imageData, outputContentType) = await ResizeImageAsync(imageData, safeContentType, width.Value, cancellationToken);
        }

        return new SafeImageResult(imageData, outputContentType, etag);
    }

    /// <summary>
    /// Computes a short, stable ETag from the raw image bytes and the requested width.
    /// </summary>
    private static string ComputeETag(byte[] data, int? width)
    {
        var hash = SHA256.HashData(data);
        var hex = Convert.ToHexString(hash)[..16];
        return width is null ? $"\"{hex}\"" : $"\"{hex}-{width}\"";
    }

    /// <summary>
    /// Resizes image data proportionally to the specified width, encoding as JPEG.
    /// Returns the original data and content type only if the image already has the requested width,
    /// or if resizing fails for any reason.
    /// </summary>
    private static async Task<(byte[] Data, string ContentType)> ResizeImageAsync(byte[] data, string contentType, int targetWidth, CancellationToken cancellationToken)
    {
        try
        {
            using var image = Image.Load(data);

            // If the image already has the requested width, return it unchanged to avoid unnecessary processing.
            if (image.Width == targetWidth)
            {
                return (data, contentType);
            }

            // Resize to the exact requested width, preserving aspect ratio and allowing upscaling if needed.
            image.Mutate(ctx => ctx.Resize(targetWidth, 0));

            using var ms = new MemoryStream();
            await image.SaveAsJpegAsync(ms, cancellationToken);
            return (ms.ToArray(), "image/jpeg");
        }
        catch (OperationCanceledException)
        {
            // Preserve cancellation semantics for the caller.
            throw;
        }
        catch (Exception)
        {
            // If resizing or encoding fails for any other reason, fall back to the original data.
            return (data, contentType);
        }
    }

    /// <summary>
    /// Normalizes and validates the stored image content type against an allowlist of safe types.
    /// Returns the normalized content type if allowed; otherwise, null.
    /// </summary>
    private static string? NormalizeAllowedImageContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        // Normalize case and trim whitespace.
        var normalized = contentType.Trim().ToLowerInvariant();

        // Map common aliases.
        if (normalized == "image/jpg")
        {
            normalized = "image/jpeg";
        }

        // Allowlist of non-scriptable image MIME types.
        return normalized switch
        {
            "image/jpeg" => "image/jpeg",
            "image/png"  => "image/png",
            "image/gif"  => "image/gif",
            "image/webp" => "image/webp",
            _            => null
        };
    }

    /// <summary>
    /// IResult implementation that returns image data with a safe content type,
    /// X-Content-Type-Options: nosniff, ETag, and Cache-Control headers.
    /// Returns 304 Not Modified when the client's cached copy is still valid.
    /// </summary>
    private sealed class SafeImageResult : IResult
    {
        private readonly byte[] _data;
        private readonly string _contentType;
        private readonly string _etag;

        public SafeImageResult(byte[] data, string contentType, string etag)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _contentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
            _etag = etag ?? throw new ArgumentNullException(nameof(etag));
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            if (httpContext is null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            var response = httpContext.Response;

            // Always set ETag and Cache-Control so conditional GETs work correctly.
            response.Headers.ETag = _etag;
            response.Headers.CacheControl = CacheControlValue;

            // If the client already has a valid cached copy, skip sending the body.
            if (httpContext.Request.Headers.IfNoneMatch
                .Any(v => string.Equals(v, _etag, StringComparison.OrdinalIgnoreCase)))
            {
                response.StatusCode = StatusCodes.Status304NotModified;
                return;
            }

            response.StatusCode = StatusCodes.Status200OK;
            response.ContentType = _contentType;
            response.Headers.XContentTypeOptions = "nosniff";

            if (_data.Length > 0)
            {
                await response.Body.WriteAsync(_data);
            }
        }
    }
}
