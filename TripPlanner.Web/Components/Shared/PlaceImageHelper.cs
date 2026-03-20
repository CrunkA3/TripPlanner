using TripPlanner.Web.Models;

namespace TripPlanner.Web.Components.Shared;

/// <summary>
/// Shared helpers for generating place image URLs and srcset attributes.
/// </summary>
internal static class PlaceImageHelper
{
    /// <summary>
    /// Returns the srcset attribute value for a saved place image (served via the API).
    /// Returns null for unsaved images that use data URLs so the srcset attribute is omitted.
    /// </summary>
    public static string? GetSrcSet(PlaceImage img) =>
        img.ImageData.Length > 0
            ? null
            : BuildSrcSet(img.Id);

    /// <summary>
    /// Returns the srcset attribute value for a place image referenced by its ID.
    /// </summary>
    public static string BuildSrcSet(string imageId) =>
        $"/api/placeImages/{imageId}?width=400 400w, /api/placeImages/{imageId}?width=800 800w, /api/placeImages/{imageId}?width=1200 1200w";
}
