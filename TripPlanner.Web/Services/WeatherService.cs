using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TripPlanner.Web.Services;

public class WeatherService(IHttpClientFactory httpClientFactory)
{
    private readonly Dictionary<string, WeatherForecast> _cache = new();

    public async Task<WeatherForecast?> GetForecastAsync(double latitude, double longitude)
    {
        var key = $"{latitude:F3},{longitude:F3}";
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        try
        {
            var client = httpClientFactory.CreateClient();
            var url = $"https://api.open-meteo.com/v1/forecast" +
                      $"?latitude={latitude.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}" +
                      $"&longitude={longitude.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}" +
                      "&daily=temperature_2m_max,temperature_2m_min,precipitation_sum,weathercode" +
                      "&timezone=auto&forecast_days=14";

            var response = await client.GetFromJsonAsync<OpenMeteoResponse>(url);
            if (response?.Daily == null)
                return null;

            var daily = response.Daily;
            var forecast = new WeatherForecast
            {
                Daily = daily.Time
                    .Select((dateStr, i) => new DailyWeather
                    {
                        Date = DateOnly.Parse(dateStr),
                        TempMax = daily.Temperature2mMax != null && i < daily.Temperature2mMax.Count ? daily.Temperature2mMax[i] : null,
                        TempMin = daily.Temperature2mMin != null && i < daily.Temperature2mMin.Count ? daily.Temperature2mMin[i] : null,
                        Precipitation = daily.PrecipitationSum != null && i < daily.PrecipitationSum.Count ? daily.PrecipitationSum[i] : null,
                        WeatherCode = daily.Weathercode != null && i < daily.Weathercode.Count ? daily.Weathercode[i] ?? 0 : 0,
                    })
                    .ToList()
            };

            _cache[key] = forecast;
            return forecast;
        }
        catch
        {
            return null;
        }
    }

    public async Task<DailyWeather?> GetWeatherForDateAsync(double latitude, double longitude, DateOnly date)
    {
        var forecast = await GetForecastAsync(latitude, longitude);
        return forecast?.Daily.FirstOrDefault(d => d.Date == date);
    }

    private sealed class OpenMeteoResponse
    {
        [JsonPropertyName("daily")]
        public OpenMeteoDaily? Daily { get; set; }
    }

    private sealed class OpenMeteoDaily
    {
        [JsonPropertyName("time")]
        public List<string> Time { get; set; } = [];

        [JsonPropertyName("temperature_2m_max")]
        public List<double?>? Temperature2mMax { get; set; }

        [JsonPropertyName("temperature_2m_min")]
        public List<double?>? Temperature2mMin { get; set; }

        [JsonPropertyName("precipitation_sum")]
        public List<double?>? PrecipitationSum { get; set; }

        [JsonPropertyName("weathercode")]
        public List<int?>? Weathercode { get; set; }
    }
}

public class WeatherForecast
{
    public List<DailyWeather> Daily { get; set; } = [];
}

public class DailyWeather
{
    public DateOnly Date { get; set; }
    public double? TempMax { get; set; }
    public double? TempMin { get; set; }
    public double? Precipitation { get; set; }
    public int WeatherCode { get; set; }

    public string GetIcon() => WeatherCode switch
    {
        0 => "☀️",
        1 => "🌤️",
        2 => "⛅",
        3 => "☁️",
        45 or 48 => "🌫️",
        51 or 53 or 55 => "🌦️",
        61 or 63 or 65 => "🌧️",
        71 or 73 or 75 => "❄️",
        77 => "🌨️",
        80 or 81 or 82 => "🌦️",
        85 or 86 => "🌨️",
        95 => "⛈️",
        96 or 99 => "⛈️",
        _ => "🌡️"
    };

    public string GetDescription() => WeatherCode switch
    {
        0 => "Clear sky",
        1 => "Mainly clear",
        2 => "Partly cloudy",
        3 => "Overcast",
        45 => "Fog",
        48 => "Icy fog",
        51 => "Light drizzle",
        53 => "Drizzle",
        55 => "Dense drizzle",
        61 => "Light rain",
        63 => "Rain",
        65 => "Heavy rain",
        71 => "Light snow",
        73 => "Snow",
        75 => "Heavy snow",
        77 => "Snow grains",
        80 => "Light showers",
        81 => "Showers",
        82 => "Heavy showers",
        85 => "Snow showers",
        86 => "Heavy snow showers",
        95 => "Thunderstorm",
        96 or 99 => "Thunderstorm with hail",
        _ => "Unknown"
    };

    public string GetSummary()
    {
        var icon = GetIcon();
        var tempStr = TempMax.HasValue && TempMin.HasValue
            ? $"{TempMin.Value:F0}°–{TempMax.Value:F0}°C"
            : TempMax.HasValue ? $"{TempMax.Value:F0}°C" : "";
        return string.IsNullOrEmpty(tempStr) ? icon : $"{icon} {tempStr}";
    }
}
