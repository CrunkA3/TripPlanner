namespace TripPlanner.Web.Services;

/// <summary>
/// Scoped service that holds the browser's IANA timezone and preferred language, allowing
/// server-side components and AI prompts to use the user's local timezone and language.
/// Initialized after the Blazor circuit becomes interactive via JS interop.
/// </summary>
public class BrowserTimeZoneService
{
    private TimeZoneInfo _timeZone = TimeZoneInfo.Utc;

    /// <summary>Gets whether the timezone has been initialized from the browser.</summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Gets the original IANA timezone identifier supplied by the browser (e.g. "Europe/Berlin").
    /// Falls back to <see cref="TimeZoneInfo.Utc"/>.<see cref="TimeZoneInfo.Id"/> until initialized.
    /// </summary>
    public string IanaTimeZoneId { get; private set; } = TimeZoneInfo.Utc.Id;

    /// <summary>
    /// Gets the BCP 47 language tag supplied by the browser's <c>navigator.language</c>
    /// (e.g. "de", "en-US"). Falls back to "en" until initialized.
    /// </summary>
    public string LanguageTag { get; private set; } = "en";

    /// <summary>Sets the preferred language from the browser's <c>navigator.language</c> value.</summary>
    public void SetLanguage(string languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag))
            return;

        // Normalize and validate: accept only well-formed BCP 47 tags (letters, digits, hyphens)
        // to prevent prompt-injection via crafted Accept-Language headers or navigator.language values.
        var trimmed = languageTag.Trim();
        if (trimmed.Length > 35 || !Bcp47Pattern.IsMatch(trimmed))
            return;

        LanguageTag = trimmed;
    }

    // BCP 47 language tag: one or more subtags of [A-Za-z0-9], separated by hyphens.
    // Examples: "en", "en-US", "zh-Hans-CN". Max length kept conservative at 35 chars.
    private static readonly System.Text.RegularExpressions.Regex Bcp47Pattern =
        new(@"^[A-Za-z0-9]+(-[A-Za-z0-9]+)*$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Sets the timezone from an IANA or Windows timezone identifier.</summary>
    public void SetTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return;

        // Always persist the original browser-supplied ID for display purposes.
        IanaTimeZoneId = timeZoneId;

        try
        {
            // .NET 6+ handles IANA IDs on Windows and Windows IDs on non-Windows natively.
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Fallback: browsers supply IANA IDs (e.g. "Europe/Berlin"). On older OS/runtime
            // combinations an explicit IANA→Windows conversion may be required.
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out var windowsId))
            {
                try
                {
                    _timeZone = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                }
                catch
                {
                    _timeZone = TimeZoneInfo.Utc;
                }
            }
            else
            {
                _timeZone = TimeZoneInfo.Utc;
            }
        }
        IsInitialized = true;
    }

    /// <summary>Gets the current <see cref="TimeZoneInfo"/> for the browser.</summary>
    public TimeZoneInfo GetTimeZone() => _timeZone;

    /// <summary>Gets the current local date/time in the browser's timezone.</summary>
    public DateTimeOffset GetLocalNow()
        => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _timeZone);

    /// <summary>Converts a date/time value to the browser's local timezone.</summary>
    public DateTimeOffset ConvertToLocalTime(DateTimeOffset dateTimeOffset)
        => TimeZoneInfo.ConvertTime(dateTimeOffset, _timeZone);
}
