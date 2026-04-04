using TripPlanner.Web.Models;

namespace TripPlanner.Web.Components.Shared;

/// <summary>
/// Shared helpers for generating place image URLs and srcset attributes.
/// </summary>
internal static class PlaceImageHelper
{
    /// <summary>
    /// Returns the src attribute value for a single place image at the given width.
    /// When a public share token is provided it is appended for anonymous access.
    /// </summary>
    public static string BuildSrc(string imageId, int width, string? publicToken = null)
    {
        var tokenParam = string.IsNullOrWhiteSpace(publicToken) ? string.Empty : $"&token={Uri.EscapeDataString(publicToken)}";
        return $"/api/placeImages/{imageId}?width={width}{tokenParam}";
    }

    /// <summary>
    /// Returns the srcset attribute value for a saved place image (served via the API).
    /// Returns null for unsaved images that use data URLs so the srcset attribute is omitted entirely.
    /// When a public share token is provided it is appended to each URL for anonymous access.
    /// </summary>
    public static string? GetSrcSet(PlaceImage img, string? publicToken = null) =>
        img.ImageData.Length > 0
            ? null
            : BuildSrcSet(img.Id, publicToken);

    /// <summary>
    /// Returns the srcset attribute value for a place image referenced by its ID.
    /// When a public share token is provided it is appended to each URL for anonymous access.
    /// </summary>
    public static string BuildSrcSet(string imageId, string? publicToken = null) =>
        $"{BuildSrc(imageId, 400, publicToken)} 400w, {BuildSrc(imageId, 800, publicToken)} 800w, {BuildSrc(imageId, 1200, publicToken)} 1200w";
}
