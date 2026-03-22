using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Components.Pages.Trips;

/// <summary>
/// Shared base class for trip plan pages that provides map-bounds filtering of places.
/// </summary>
public abstract class TripPlanPageBase : ComponentBase
{
    private List<Place> _allPlaces = new();

    protected List<Place> AllPlaces
    {
        get => _allPlaces;
        set
        {
            _allPlaces = value;
            InvalidateVisiblePlaces();
        }
    }

    private bool _boundsKnown;
    private double _boundsNorth;
    private double _boundsSouth;
    private double _boundsEast;
    private double _boundsWest;
    private List<Place>? _visiblePlaces;

    protected List<Place> VisiblePlaces => _visiblePlaces ??= ComputeVisiblePlaces();

    protected bool BoundsKnown => _boundsKnown;

    private List<Place> ComputeVisiblePlaces() =>
        _boundsKnown
            ? AllPlaces.Where(p => IsInBounds(p.Latitude, p.Longitude)).ToList()
            : AllPlaces;

    protected void InvalidateVisiblePlaces()
    {
        _visiblePlaces = null;
    }

    private bool IsInBounds(double lat, double lng)
    {
        if (lat < _boundsSouth || lat > _boundsNorth) return false;
        // Handle antimeridian crossing (west > east when map spans the date line)
        return _boundsWest <= _boundsEast
            ? lng >= _boundsWest && lng <= _boundsEast
            : lng >= _boundsWest || lng <= _boundsEast;
    }

    [JSInvokable]
    public async Task OnBoundsChanged(double north, double south, double east, double west)
    {
        _boundsNorth = north;
        _boundsSouth = south;
        _boundsEast = east;
        _boundsWest = west;
        _boundsKnown = true;
        InvalidateVisiblePlaces();
        await InvokeAsync(StateHasChanged);
    }
}
