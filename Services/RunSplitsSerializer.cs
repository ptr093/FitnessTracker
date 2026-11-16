using System.Globalization;
using System.Text.Json;

namespace FitnessTracker.Services;

/// <summary>
/// Jeden format międzyczasów w bazie: lista prędkości w km/h, np. [12.0, 12.4].
/// Odczyt obsługuje też stary format z ręcznego wpisu: [{"Kilometer":1,"Pace":"5:00"}].
/// </summary>
public static class RunSplitsSerializer
{
    public static string Serialize(IEnumerable<double> speedsKmH) =>
        JsonSerializer.Serialize(speedsKmH.Select(s => Math.Round(s, 2)).ToList());

    public static List<double> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<double>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return new List<double>();

            var result = new List<double>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number)
                {
                    result.Add(item.GetDouble());
                }
                else if (item.ValueKind == JsonValueKind.Object &&
                         item.TryGetProperty("Pace", out var pace) &&
                         pace.ValueKind == JsonValueKind.String &&
                         TryPaceToSpeed(pace.GetString(), out var speed))
                {
                    result.Add(Math.Round(speed, 2));
                }
            }

            return result;
        }
        catch (JsonException)
        {
            return new List<double>();
        }
    }

    /// <summary>Tempo "m:ss" (min/km) → prędkość w km/h.</summary>
    public static bool TryPaceToSpeed(string? pace, out double speedKmH)
    {
        speedKmH = 0;
        if (string.IsNullOrWhiteSpace(pace))
            return false;

        var parts = pace.Trim().Replace('.', ':').Replace(',', ':').Split(':');
        if (parts.Length is < 1 or > 2 ||
            !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes))
            return false;

        var seconds = 0;
        if (parts.Length == 2 &&
            (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out seconds) || seconds >= 60))
            return false;

        var totalMinutes = minutes + seconds / 60.0;
        if (totalMinutes <= 0)
            return false;

        speedKmH = 60.0 / totalMinutes;
        return true;
    }
}
