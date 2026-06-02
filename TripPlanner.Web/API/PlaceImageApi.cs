using System.Security.Claims;
using System.Security.Cryptography;
using SkiaSharp;
using TripPlanner.Web.Models;
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
            .WithDisplayName("Place Image API");

        // Endpoint to retrieve a place image by its ID.
        // Optional 'width' query parameter resizes the image proportionally (allowed: 400, 800, 1200).
        // Access is granted to authenticated users who own the image, or to anyone with a valid
        // public collection share token that includes the image's place.
        groupPlaceImages.MapGet("/{imageId}", GetPlaceImageAsync)
            .WithName("GetPlaceImage")
            .WithDisplayName("Get Place Image")
            .AllowAnonymous()
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg", additionalContentTypes: "image/png")
            .Produces(StatusCodes.Status304NotModified)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);


        return groupPlaceImages;
    }


    private static async Task<IResult> GetPlaceImageAsync(string imageId, int? width, string? token, ClaimsPrincipal user, IPlaceRepository placeRepository, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageId))
        {
            return Results.BadRequest("Image ID cannot be null or empty.");
        }

        if (width is not null && !AllowedWidths.Contains(width.Value))
        {
            return Results.BadRequest($"Invalid width. Allowed values: {string.Join(", ", AllowedWidths)}.");
        }

        PlaceImage? placeImage;
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is not null)
        {
            // Authenticated: check ownership via user ID
            placeImage = await placeRepository.GetPlaceImageAsync(imageId, userId, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(token))
        {
            // Anonymous with a public share token: check collection membership
            placeImage = await placeRepository.GetPlaceImageByPublicTokenAsync(imageId, token, cancellationToken);
        }
        else
        {
            return Results.Unauthorized();
        }
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
        if (imageData is not null && width is not null)
        {
            (imageData, outputContentType) = ResizeImage(imageData!, safeContentType, width.Value, cancellationToken);
        }

        if (imageData is null || outputContentType is null)
        {
            // If resizing failed, fall back to the original data and content type.
            imageData = placeImage.ImageData;
            outputContentType = safeContentType;
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
    private static (byte[]? Data, string? ContentType) ResizeImage(byte[] data, string contentType, int targetWidth, CancellationToken cancellationToken)
    {
        try
        {
            // SkiaSharp resize APIs are synchronous; cancellation can only be honored before processing starts.
            cancellationToken.ThrowIfCancellationRequested();
            using var image = SKBitmap.Decode(data);
            if (image is null)
            {
                return (null, null);
            }

            // If the image already has the requested width, return it unchanged to avoid unnecessary processing.
            if (image.Width == targetWidth)
            {
                return (data, contentType);
            }

            // Resize to the exact requested width, preserving aspect ratio and allowing upscaling if needed.
            var targetHeight = Math.Max(1, (int)Math.Round((double)image.Height * targetWidth / image.Width));
            using var resized = image.Resize(new SKImageInfo(targetWidth, targetHeight), SKFilterQuality.High);
            if (resized is null)
            {
                return (data, contentType);
            }

            using var outputImage = SKImage.FromBitmap(resized);
            using var encoded = outputImage.Encode(SKEncodedImageFormat.Jpeg, quality: 90);
            if (encoded is null)
            {
                return (data, contentType);
            }

            return (encoded.ToArray(), "image/jpeg");
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
    internal static string? NormalizeAllowedImageContentType(string? contentType)
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
            "image/png" => "image/png",
            "image/gif" => "image/gif",
            "image/webp" => "image/webp",
            _ => null
        };
    }

    /// <summary>
    /// IResult implementation that returns image data with a safe content type,
    /// X-Content-Type-Options: nosniff, ETag, and Cache-Control headers.
    /// Returns 304 Not Modified when the client's cached copy is still valid.
    /// </summary>
    private sealed class SafeImageResult(byte[] data, string contentType, string etag) : IResult
    {
        private readonly byte[] _data = data ?? throw new ArgumentNullException(nameof(data));
        private readonly string _contentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
        private readonly string _etag = etag ?? throw new ArgumentNullException(nameof(etag));

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

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
