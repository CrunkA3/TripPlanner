using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using ReverseMarkdown;

namespace TripPlanner.Web.Services;

/// <summary>
/// Shared helpers used by both <see cref="OllamaPlaceAnalysisService"/> and
/// <see cref="OpenAI.OpenAIPlaceAnalysisService"/> to prepare web-page content for LLM analysis.
/// </summary>
internal static class PlaceAnalysisHelpers
{
    // Elements whose full content (including text) must be removed before Markdown conversion.
    private static readonly string[] StrippedElements = ["script", "style", "head", "noscript"];

    /// <summary>
    /// Converts raw HTML to structured Markdown, removing non-content elements first.
    /// Truncates the result to <paramref name="maxLength"/> characters.
    /// </summary>
    internal static string ExtractTextFromHtml(string html, int maxLength)
    {
        // Use HtmlAgilityPack to remove non-content nodes (script, style, head, noscript)
        // so their text content never leaks into the Markdown/LLM prompt.
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        foreach (var tag in StrippedElements)
        {
            var nodes = doc.DocumentNode.SelectNodes($"//{tag}");
            if (nodes != null)
                foreach (var node in nodes.ToArray())
                    node.Remove();
        }

        // Convert the cleaned HTML to Markdown to preserve structure (headings, lists, links)
        var converter = new Converter(new Config
        {
            UnknownTags = Config.UnknownTagsOption.Drop,
            SmartHrefHandling = true,
        });
        var markdown = converter.Convert(doc.DocumentNode.OuterHtml);

        // Normalize whitespace
        markdown = Regex.Replace(markdown, @"\n{3,}", "\n\n").Trim();

        // Truncate to a manageable size for the LLM
        return markdown.Length > maxLength ? markdown[..maxLength] : markdown;
    }

    /// <summary>
    /// Scans the raw HTML for <c>href</c> attributes pointing to GPX files and returns
    /// their absolute http/https URLs, resolved against <paramref name="pageUrl"/>.
    /// </summary>
    internal static List<string> ExtractGpxUrls(string html, string pageUrl)
    {
        var baseUri = Uri.TryCreate(pageUrl, UriKind.Absolute, out var u) ? u : null;
        var results = new List<string>();

        foreach (Match m in Regex.Matches(html, @"href\s*=\s*[""']([^""']*\.gpx[^""']*)[""']",
            RegexOptions.IgnoreCase))
        {
            // HTML-decode the attribute value so entities like &amp; become & in the URL.
            var href = WebUtility.HtmlDecode(m.Groups[1].Value);

            Uri? resolved = null;
            if (Uri.TryCreate(href, UriKind.Absolute, out var absUri))
                resolved = absUri;
            else if (baseUri != null && Uri.TryCreate(baseUri, href, out var relResolved))
                resolved = relResolved;

            // Restrict to http/https to avoid mailto:, javascript:, file:, and other unsafe schemes.
            if (resolved != null &&
                (resolved.Scheme == Uri.UriSchemeHttp || resolved.Scheme == Uri.UriSchemeHttps))
                results.Add(resolved.ToString());
        }

        return results.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
