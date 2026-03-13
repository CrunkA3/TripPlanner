namespace TripPlanner.Web.Services;

/// <summary>
/// Scoped service that holds the browser's IANA timezone, allowing server-side
/// components to display date/time values in the user's local timezone.
/// Initialized after the Blazor circuit becomes interactive via JS interop.
/// </summary>
public class BrowserTimeZoneService
{
    private TimeZoneInfo _timeZone = TimeZoneInfo.Utc;

    /// <summary>Gets whether the timezone has been initialized from the browser.</summary>
    public bool IsInitialized { get; private set; }

    /// <summary>Sets the timezone from an IANA or Windows timezone identifier.</summary>
    public void SetTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return;

        try
        {
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            _timeZone = TimeZoneInfo.Utc;
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
