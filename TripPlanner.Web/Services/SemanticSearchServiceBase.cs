using Microsoft.Extensions.Caching.Memory;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Services;

/// <summary>
/// Base class for semantic search implementations.
/// Handles embedding caching (via <see cref="IMemoryCache"/>), batched API calls, and cosine-similarity ranking.
/// Derived classes only need to implement <see cref="GetEmbeddingsAsync"/>.
/// </summary>
public abstract class SemanticSearchServiceBase(IMemoryCache cache, ILogger logger) : ISemanticSearchService
{
    private const int MinimumFallbackResults = 5;
    private const int FallbackPercentageDivisor = 5;
    // Threshold below which a place is considered semantically irrelevant.
    private const double SimilarityThreshold = 0.25;
    private static readonly TimeSpan EmbeddingCacheExpiry = TimeSpan.FromHours(24);

    /// <summary>
    /// Fetches embeddings for a batch of texts in a single API request.
    /// Returns <c>null</c> if the call fails; individual entries may be zero-length arrays on partial failure.
    /// </summary>
    protected abstract Task<float[][]?> GetEmbeddingsAsync(string[] texts, CancellationToken ct);

    /// <summary>
    /// The embeddings model name used by this implementation (e.g. "text-embedding-3-small").
    /// Included in cache keys so that changing the model or provider invalidates existing cached vectors.
    /// </summary>
    protected abstract string EmbeddingsModelName { get; }

    public async Task<IReadOnlyList<Place>> SearchAsync(string query, IEnumerable<Place> places, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return places.ToList();

        var placeList = places.ToList();
        if (placeList.Count == 0)
            return placeList;

        // Embed the search query.
        var queryEmbResult = await GetEmbeddingsAsync([query], ct);
        if (queryEmbResult is null || queryEmbResult.Length == 0 || queryEmbResult[0].Length == 0)
        {
            logger.LogWarning("Semantic search: could not obtain query embedding; failing semantic search.");
            throw new InvalidOperationException("Semantic search failed: could not obtain query embedding.");
        }
        var queryEmb = queryEmbResult[0];

        // Load per-place embeddings, fetching uncached ones in a single batch call.
        var placeEmbs = new float[placeList.Count][];
        var uncachedIdx = new List<int>();
        var uncachedTexts = new List<string>();

        for (var i = 0; i < placeList.Count; i++)
        {
            var key = BuildCacheKey(placeList[i], EmbeddingsModelName);
            if (cache.TryGetValue(key, out float[]? cached) && cached is { Length: > 0 })
                placeEmbs[i] = cached;
            else
            {
                uncachedIdx.Add(i);
                uncachedTexts.Add(BuildPlaceText(placeList[i]));
            }
        }

        if (uncachedTexts.Count > 0)
        {
            var batchResult = await GetEmbeddingsAsync([.. uncachedTexts], ct);
            if (batchResult is not null)
            {
                for (var j = 0; j < uncachedIdx.Count && j < batchResult.Length; j++)
                {
                    var idx = uncachedIdx[j];
                    var emb = batchResult[j];
                    if (emb is { Length: > 0 })
                    {
                        placeEmbs[idx] = emb;
                        cache.Set(BuildCacheKey(placeList[idx], EmbeddingsModelName), emb, new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = EmbeddingCacheExpiry,
                            Size = emb.Length
                        });
                    }
                }
            }
        }

        // Score every place by cosine similarity to the query.
        var scored = placeList
            .Select((p, i) => (Place: p, Score: placeEmbs[i] is { Length: > 0 }
                ? CosineSimilarity(queryEmb, placeEmbs[i])
                : 0.0))
            .OrderByDescending(x => x.Score)
            .ToList();

        // Return places above threshold; if none qualify keep the top results so the page isn't empty.
        var aboveThreshold = scored.Where(x => x.Score >= SimilarityThreshold).Select(x => x.Place).ToList();
        return aboveThreshold.Count > 0
            ? aboveThreshold
            : scored.Take(Math.Max(MinimumFallbackResults, placeList.Count / FallbackPercentageDivisor)).Select(x => x.Place).ToList();
    }

    private static string BuildPlaceText(Place p)
    {
        var parts = new List<string> { p.Name };
        if (!string.IsNullOrWhiteSpace(p.Description))
            parts.Add(p.Description);
        if (p.Tags?.Count > 0)
            parts.Add(string.Join(", ", p.Tags));
        return string.Join(". ", parts);
    }

    private static string BuildCacheKey(Place p, string modelName)
        => $"place_emb:{modelName}:{p.Id}:{p.UpdatedAt?.Ticks ?? p.CreatedAt.Ticks}";

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom < 1e-10 ? 0 : dot / denom;
    }
}
