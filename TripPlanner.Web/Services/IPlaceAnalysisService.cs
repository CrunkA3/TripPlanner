using TripPlanner.Web.Models;

namespace TripPlanner.Web.Services;

public interface IPlaceAnalysisService
{
    Task<PlaceAnalysisResult?> AnalyzeUrlAsync(string url, string? language = null, CancellationToken cancellationToken = default);
}
