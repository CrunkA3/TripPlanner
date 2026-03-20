using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using TripPlanner.Web.Repositories;
using TripPlanner.Web.Services;

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
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg", additionalContentTypes: "image/png")
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);


        return groupPlaceImages;
    }


    private static async Task<IResult> GetPlaceImageAsync(string imageId, ClaimsPrincipal user, IPlaceRepository placeRepository, CancellationToken cancellationToken)
    {
        // For demonstration purposes, we'll return a placeholder image for any valid imageId.
        // In a real implementation, you would retrieve the image from a database or file storage based on the imageId.
        if (string.IsNullOrWhiteSpace(imageId))
        {
            return Results.BadRequest("Image ID cannot be null or empty.");
        }

        // Get the current user (you would typically get this from the authentication context)
        var currentUser = user.Identities;
        if (currentUser is null)
        {
            return Results.BadRequest("User not authenticated.");
        }

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            return Results.BadRequest("User not authenticated.");
        }

        // Check if the image exists and belongs to the current user
        var placeImage = await placeRepository.GetPlaceImageAsync(imageId, userId);
        if (placeImage is null)
        {
            return Results.NotFound();
        }

        return Results.File(placeImage.ImageData, placeImage.ImageContentType);
    }



}
