using Microsoft.FluentUI.AspNetCore.Components;
using Icons = Microsoft.FluentUI.AspNetCore.Components.Icons;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Repositories;

public interface IPlaceRepository
{
    Task<List<Place>> GetAllByUserAsync(string userId);
    Task<List<Place>> GetAllWithAnyWishlistAsync(string userId);
    Task<List<Place>> GetAllForTripAsync(string tripId);


    Task<Place?> GetByIdAsync(string id, string userId);
    Task<Place> AddAsync(Place place);
    Task<Place> UpdateAsync(Place place);
    Task DeleteAsync(string id);
    Task<List<Place>> FilterAsync(PlaceCategory? category = null, List<string>? tags = null, bool? hasGpxTrack = null);
    Task<List<Place>> GetByWishlistIdAsync(string wishlistId);

    static string GetCategoryColor(PlaceCategory category)
    {
        return category switch
        {
            PlaceCategory.Viewpoint => "#4285F4",
            PlaceCategory.Museum => "#EA4335",
            PlaceCategory.Restaurant => "#FBBC04",
            PlaceCategory.Nature => "#34A853",
            PlaceCategory.Activity => "#00ACC1",
            PlaceCategory.Accommodation => "#9C27B0",
            PlaceCategory.Shopping => "#FF6F00",
            PlaceCategory.Entertainment => "#E91E63",
            PlaceCategory.Race => "#F44336",
            _ => "#757575"
        };
    }

    static string GetCategoryClass(PlaceCategory category) => category switch
    {
        PlaceCategory.Viewpoint => "place-cat-viewpoint",
        PlaceCategory.Museum => "place-cat-museum",
        PlaceCategory.Restaurant => "place-cat-restaurant",
        PlaceCategory.Nature => "place-cat-nature",
        PlaceCategory.Activity => "place-cat-activity",
        PlaceCategory.Accommodation => "place-cat-accommodation",
        PlaceCategory.Shopping => "place-cat-shopping",
        PlaceCategory.Entertainment => "place-cat-entertainment",
        PlaceCategory.Race => "place-cat-race",
        _ => "place-cat-other",
    };


    static Icon GetCategoryIcon(PlaceCategory category) => category switch
    {
        PlaceCategory.Viewpoint => new Icons.Regular.Size32.Globe(),
        PlaceCategory.Museum => new Icons.Regular.Size32.Building(),
        PlaceCategory.Restaurant => new Icons.Regular.Size32.Food(),
        PlaceCategory.Nature => new Icons.Regular.Size32.LeafTwo(),
        PlaceCategory.Activity => new Icons.Regular.Size32.Run(),
        PlaceCategory.Accommodation => new Icons.Regular.Size32.BuildingHome(),
        PlaceCategory.Shopping => new Icons.Regular.Size32.ShoppingBag(),
        PlaceCategory.Entertainment => new Icons.Regular.Size32.StarEmphasis(),
        PlaceCategory.Race => new Icons.Regular.Size32.Flag(),
        _ => new Icons.Regular.Size32.LocationArrow(),
    };
}
