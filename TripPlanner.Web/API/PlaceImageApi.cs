using System.Security.Claims;
using TripPlanner.Web.Repositories;

namespace TripPlanner.Web.API;

internal static class PlaceImageApi
{
    internal static IEndpointConventionBuilder MapPlaceImageApi(this IEndpointRouteBuilder endpoints)
    {
        var groupPlaceImages = endpoints.MapGroup("/api/placeImages")
            .WithDisplayName("Place Image API");

        // Endpoint to retrieve a place image by its ID
        groupPlaceImages.MapGet("/{imageId}", GetPlaceImageAsync)
            .WithName("GetPlaceImage")
            .WithDisplayName("Get Place Image")
            .RequireAuthorization()
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg", additionalContentTypes: "image/png")
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

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        // Check if the image exists and belongs to the current user
        var placeImage = await placeRepository.GetPlaceImageAsync(imageId, userId, cancellationToken);
        if (placeImage is null)
        {
            return Results.NotFound();
        }

        return Results.File(placeImage.ImageData, placeImage.ImageContentType);
    }



}
