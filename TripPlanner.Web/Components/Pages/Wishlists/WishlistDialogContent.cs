using TripPlanner.Web.Models;

namespace TripPlanner.Web.Components.Pages.Wishlists;

public record CreateWishlistDialogContent(Wishlist Wishlist);

public record ShareWishlistDialogContent(string WishlistId, string WishlistName);
