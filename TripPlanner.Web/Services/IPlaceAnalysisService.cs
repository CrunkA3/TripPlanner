using TripPlanner.Web.Models;

namespace TripPlanner.Web.Services;

public interface IPlaceAnalysisService
{
    Task<PlaceSuggestion?> AnalyzeUrlAsync(string url, CancellationToken cancellationToken = default);
}
