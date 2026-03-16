using System.Text.RegularExpressions;
using ReverseMarkdown;

namespace TripPlanner.Web.Services;

/// <summary>
/// Shared helpers used by both <see cref="OllamaPlaceAnalysisService"/> and
/// <see cref="OpenAI.OpenAIPlaceAnalysisService"/> to prepare web-page content for LLM analysis.
/// </summary>
internal static class PlaceAnalysisHelpers
{
    /// <summary>
    /// Converts raw HTML to structured Markdown, stripping scripts/styles/head first.
    /// Truncates the result to <paramref name="maxLength"/> characters.
    /// </summary>
    internal static string ExtractTextFromHtml(string html, int maxLength)
    {
        // Remove script, style, and head blocks including their content
        html = Regex.Replace(html, @"<(script|style|head)[^>]*>.*?</(script|style|head)>",
            string.Empty, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        // Convert HTML to Markdown to preserve structure (headings, lists, links)
        var converter = new Converter(new Config
        {
            UnknownTags = Config.UnknownTagsOption.Drop,
            SmartHrefHandling = true,
        });
        var markdown = converter.Convert(html);

        // Normalize whitespace
        markdown = Regex.Replace(markdown, @"\n{3,}", "\n\n").Trim();

        // Truncate to a manageable size for the LLM
        return markdown.Length > maxLength ? markdown[..maxLength] : markdown;
    }

    /// <summary>
    /// Scans the raw HTML for <c>href</c> attributes pointing to GPX files and returns
    /// their absolute URLs, resolved against <paramref name="pageUrl"/>.
    /// </summary>
    internal static List<string> ExtractGpxUrls(string html, string pageUrl)
    {
        var baseUri = Uri.TryCreate(pageUrl, UriKind.Absolute, out var u) ? u : null;
        var results = new List<string>();

        foreach (Match m in Regex.Matches(html, @"href\s*=\s*[""']([^""']*\.gpx[^""']*)[""']",
            RegexOptions.IgnoreCase))
        {
            var href = m.Groups[1].Value;
            if (Uri.TryCreate(href, UriKind.Absolute, out var absUri))
            {
                results.Add(absUri.ToString());
            }
            else if (baseUri != null && Uri.TryCreate(baseUri, href, out var resolved))
            {
                results.Add(resolved.ToString());
            }
        }

        return results.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
