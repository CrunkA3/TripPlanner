using TripPlanner.Web.Models;

namespace TripPlanner.Web.Services;

public interface IPlaceAnalysisService
{
    Task<PlaceAnalysisResult?> AnalyzeUrlAsync(string url, string languageTag = "en", CancellationToken cancellationToken = default);
}
