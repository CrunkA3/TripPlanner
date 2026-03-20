using TripPlanner.Web.Models;

namespace TripPlanner.Web.Components.Shared;

/// <summary>
/// Shared helpers for generating place image URLs and srcset attributes.
/// </summary>
internal static class PlaceImageHelper
{
    /// <summary>
    /// Returns the srcset attribute value for a saved place image (served via the API).
    /// Returns an empty string for unsaved images that use data URLs.
    /// </summary>
    public static string GetSrcSet(PlaceImage img) =>
        img.ImageData.Length > 0
            ? string.Empty
            : BuildSrcSet(img.Id);

    /// <summary>
    /// Returns the srcset attribute value for a place image referenced by its ID.
    /// </summary>
    public static string BuildSrcSet(string imageId) =>
        $"/api/placeImages/{imageId}?width=400 400w, /api/placeImages/{imageId}?width=800 800w";
}
