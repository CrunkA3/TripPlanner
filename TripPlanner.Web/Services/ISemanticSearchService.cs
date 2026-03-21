using TripPlanner.Web.Models;

namespace TripPlanner.Web.Services;

/// <summary>
/// Provides AI-powered semantic search over a collection of places using text embeddings.
/// </summary>
public interface ISemanticSearchService
{
    /// <summary>
    /// Ranks <paramref name="places"/> by semantic similarity to <paramref name="query"/>
    /// and returns them in descending relevance order.
    /// Falls back to the original ordering when embeddings are unavailable.
    /// </summary>
    Task<IReadOnlyList<Place>> SearchAsync(string query, IEnumerable<Place> places, CancellationToken ct = default);
}
