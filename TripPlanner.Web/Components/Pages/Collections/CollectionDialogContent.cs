using TripPlanner.Web.Models;

namespace TripPlanner.Web.Components.Pages.Collections;

public record CreateCollectionDialogContent(PlaceCollection Collection);

public record AddToCollectionDialogContent(string PlaceId, string PlaceName);
