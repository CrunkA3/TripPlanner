using System.Security.Claims;
using TripPlanner.Web.Repositories;

namespace TripPlanner.Web.API;

internal static class PlaceImageApi
{
    internal static IEndpointConventionBuilder MapPlaceImageApi(this IEndpointRouteBuilder endpoints)
    {
        var groupPlaceImages = endpoints.MapGroup("/api/placeImages")
            .WithDisplayName("Place Image API")
            .RequireAuthorization();

        // Endpoint to retrieve a place image by its ID
        groupPlaceImages.MapGet("/{imageId}", GetPlaceImageAsync)
            .WithName("GetPlaceImage")
            .WithDisplayName("Get Place Image")
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg", additionalContentTypes: "image/png")
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);


        return groupPlaceImages;
    }


    private static async Task<IResult> GetPlaceImageAsync(string imageId, ClaimsPrincipal user, IPlaceRepository placeRepository, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageId))
        {
            return Results.BadRequest("Image ID cannot be null or empty.");
        }

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)!.Value;

        // Check if the image exists and belongs to the current user
        var placeImage = await placeRepository.GetPlaceImageAsync(imageId, userId);
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

        return new SafeImageResult(placeImage.ImageData, safeContentType);
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
    /// IResult implementation that returns image data with a safe content type and
    /// adds the X-Content-Type-Options: nosniff header.
    /// </summary>
    private sealed class SafeImageResult : IResult
    {
        private readonly byte[] _data;
        private readonly string _contentType;

        public SafeImageResult(byte[] data, string contentType)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _contentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            if (httpContext is null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            var response = httpContext.Response;
            response.StatusCode = StatusCodes.Status200OK;
            response.ContentType = _contentType;
            response.Headers["X-Content-Type-Options"] = "nosniff";

            if (_data.Length > 0)
            {
                await response.Body.WriteAsync(_data, 0, _data.Length);
            }
        }
    }
}
