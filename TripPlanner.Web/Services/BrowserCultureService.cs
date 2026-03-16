namespace TripPlanner.Web.Services;

/// <summary>
/// Scoped service that holds the browser's preferred language (e.g. "de", "en-US"),
/// allowing server-side AI prompts to respond in the user's language.
/// Initialized after the Blazor circuit becomes interactive via JS interop.
/// </summary>
public class BrowserCultureService
{
    /// <summary>Gets whether the culture has been initialized from the browser.</summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Gets the BCP 47 language tag supplied by the browser (e.g. "de", "en-US", "fr-FR").
    /// Falls back to "en" until initialized.
    /// </summary>
    public string LanguageTag { get; private set; } = "en";

    /// <summary>Sets the language tag from the browser's <c>navigator.language</c> value.</summary>
    public void SetLanguage(string languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag))
            return;

        LanguageTag = languageTag;
        IsInitialized = true;
    }
}
