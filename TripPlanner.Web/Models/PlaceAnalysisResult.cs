namespace TripPlanner.Web.Models;

/// <summary>
/// Wraps the result of an AI place analysis, including the suggestion and the raw prompt/response.
/// </summary>
public class PlaceAnalysisResult
{
    /// <summary>The structured place data extracted by the AI, or null if extraction failed.</summary>
    public PlaceSuggestion? Suggestion { get; init; }

    /// <summary>The full prompt that was sent to the AI model.</summary>
    public string Prompt { get; init; } = string.Empty;

    /// <summary>The raw text response returned by the AI model.</summary>
    public string RawResponse { get; init; } = string.Empty;
}
