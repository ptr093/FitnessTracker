using Dynastream.Fit;
using FitnessTracker.Models;
using System.Text.Json;
using DateTime = System.DateTime;

namespace FitnessTracker.Services;

/// <summary>
/// Import aktywności z plików .FIT (Garmin) przy użyciu oficjalnego Garmin FIT SDK.
/// Obsługuje biegi (dystans, czas, międzyczasy z okrążeń, tętno) oraz treningi siłowe
/// nagrane na zegarku (serie z ciężarem i powtórzeniami).
/// </summary>
public class FitImportService
{
    private readonly DatabaseService _databaseService;

    public FitImportService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<FitImportResult?> PickAndImportAsync()
    {
        var files = await FilePicker.Default.PickMultipleAsync(new PickOptions
        {
            PickerTitle = "Wybierz pliki .FIT z Garmina",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.Android] = new[] { "application/octet-stream", "application/vnd.ant.fit", "*/*" },
                [DevicePlatform.iOS] = new[] { "public.data" },
                [DevicePlatform.MacCatalyst] = new[] { "public.data" },
                [DevicePlatform.WinUI] = new[] { ".fit" }
            })
        });

        var list = files?.Where(f => f != null).ToList();
        if (list == null || list.Count == 0)
            return null;

        var result = new FitImportResult();

        foreach (var file in list)
        {
            try
            {
                await using var source = await file.OpenReadAsync();
                // Dekoder FIT wymaga strumienia z możliwością przewijania
                using var stream = new MemoryStream();
                await source.CopyToAsync(stream);
                stream.Position = 0;

                await ImportStreamAsync(stream, file.FileName, result);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{file.FileName}: {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>
    /// Importuje jeden plik .FIT (strumień z możliwością przewijania). Używane przez wybór plików
    /// i przez <see cref="GarminApiService"/>.
    /// </summary>
    public async Task ImportStreamAsync(Stream stream, string sourceName, FitImportResult result)
    {
        var workouts = Parse(stream);
        if (workouts.Count == 0)
        {
            result.Unsupported.Add(sourceName);
            return;
        }

        foreach (var workout in workouts)
        {
            if (workout.ExternalId != null && await _databaseService.ExistsByExternalIdAsync(workout.ExternalId))
            {
                result.Duplicates++;
                continue;
            }

            await _databaseService.SaveWorkoutAsync(workout);
            if (workout.Type == "Bieganie") result.Runs++;
            else result.GymExercises++;
        }
    }

    public static List<Workout> Parse(Stream stream)
    {
        var decoder = new Decode();
        if (!decoder.IsFIT(stream))
            throw new InvalidDataException("To nie jest plik FIT.");
        stream.Position = 0;

        var listener = new FitListener();
        decoder.MesgEvent += listener.OnMesg;
        decoder.Read(stream);

        var messages = listener.FitMessages;
        var session = messages.SessionMesgs.FirstOrDefault();
        if (session == null)
            return new List<Workout>();

        var fileId = messages.FileIdMesgs.FirstOrDefault();
        var start = ToLocal(session.GetStartTime()) ?? ToLocal(fileId?.GetTimeCreated()) ?? DateTime.Now;
        var baseId = $"fit:{fileId?.GetSerialNumber()}:{start:yyyyMMddHHmmss}";
        var sport = session.GetSport();

        if (sport == Sport.Running || sport == Sport.Walking)
            return new List<Workout> { ParseRun(session, messages.LapMesgs, start, baseId) };

        if (messages.SetMesgs.Count > 0)
            return ParseStrength(messages.SetMesgs, start, baseId);

        return new List<Workout>();
    }

    private static Workout ParseRun(SessionMesg session, IEnumerable<LapMesg> laps, DateTime start, string externalId)
    {
        var distanceKm = (session.GetTotalDistance() ?? 0) / 1000.0;
        var timeMin = (session.GetTotalTimerTime() ?? 0) / 60.0;

        // Garmin domyślnie robi auto-okrążenie co 1 km – traktujemy okrążenia jako międzyczasy (km/h)
        var speeds = laps
            .Select(l => (Dist: l.GetTotalDistance() ?? 0, Time: l.GetTotalTimerTime() ?? 0))
            .Where(l => l.Dist >= 500 && l.Time > 0)
            .Select(l => Math.Round(l.Dist / l.Time * 3.6, 2))
            .ToList();

        return new Workout
        {
            Type = "Bieganie",
            Date = start,
            Distance = Math.Round(distanceKm, 2),
            Duration = Math.Round(timeMin, 1),
            AverageHeartRate = session.GetAvgHeartRate(),
            SplitsJson = JsonSerializer.Serialize(speeds),
            Exercise = "Bieg (Garmin)",
            ExternalId = externalId
        };
    }

    private static List<Workout> ParseStrength(IEnumerable<SetMesg> sets, DateTime start, string baseId)
    {
        // Bierzemy tylko serie robocze (nie odpoczynek) z powtórzeniami
        var activeSets = sets
            .Where(s => s.GetSetType() == SetType.Active && (s.GetRepetitions() ?? 0) > 0)
            .Select(s => new
            {
                Category = s.GetNumCategory() > 0 ? s.GetCategory(0) : null,
                Weight = Math.Round((double)(s.GetWeight() ?? 0), 1),
                Reps = (int)(s.GetRepetitions() ?? 0)
            })
            .ToList();

        // Kolejne serie tej samej kategorii łączymy w jedno ćwiczenie (kolejność zachowana)
        var result = new List<Workout>();
        var index = 0;
        foreach (var group in activeSets.GroupBy(s => s.Category))
        {
            var (category, exercise) = MapExercise(group.Key);
            var workoutSets = group
                .Select((s, i) => new WorkoutSet { SetNumber = i + 1, Weight = s.Weight, Reps = s.Reps })
                .ToList();

            result.Add(new Workout
            {
                Type = "Siłownia",
                Date = start.AddSeconds(index), // różne znaczniki czasu – zachowuje kolejność w historii
                Category = category,
                Exercise = exercise,
                GymSetsJson = JsonSerializer.Serialize(workoutSets),
                ExternalId = $"{baseId}:{group.Key?.ToString() ?? "x"}"
            });
            index++;
        }

        return result;
    }

    /// <summary>
    /// Mapuje kategorię ćwiczenia z FIT na partię i nazwę z katalogu aplikacji,
    /// żeby rekordy i wykresy łączyły się z treningami wpisanymi ręcznie.
    /// </summary>
    private static (string Category, string Exercise) MapExercise(ushort? fitCategory) => fitCategory switch
    {
        ExerciseCategory.BenchPress => ("Klatka piersiowa", "🏋️ Wyciskanie sztangi leżąc"),
        ExerciseCategory.Flye => ("Klatka piersiowa", "🦋 Rozpiętki hantlami"),
        ExerciseCategory.PushUp => ("Klatka piersiowa", "⌚ Pompki (Garmin)"),
        ExerciseCategory.Deadlift => ("Plecy", "🔥 Martwy ciąg"),
        ExerciseCategory.Row => ("Plecy", "🚣 Wiosłowanie sztangą"),
        ExerciseCategory.PullUp => ("Plecy", "🧗 Podciąganie na drążku"),
        ExerciseCategory.Squat => ("Nogi", "🦵 Przysiad ze sztangą (Squat)"),
        ExerciseCategory.Lunge => ("Nogi", "🚶 Wykroki"),
        ExerciseCategory.LegCurl => ("Nogi", "🦵 Uginanie nóg na maszynie"),
        ExerciseCategory.CalfRaise => ("Nogi", "🦵 Wznosy na łydki"),
        ExerciseCategory.HipRaise => ("Nogi", "🍑 Hip Thrust (Unoszenie bioder)"),
        ExerciseCategory.ShoulderPress => ("Barki", "🛡️ Wyciskanie żołnierskie (OHP)"),
        ExerciseCategory.LateralRaise => ("Barki", "🦅 Wznosy bokiem"),
        ExerciseCategory.Curl => ("Ramiona", "💪 Uginanie ze sztangą (Biceps)"),
        ExerciseCategory.TricepsExtension => ("Ramiona", "🪢 Prostowanie z linką wyciągu"),
        ExerciseCategory.Plank => ("Brzuch", "🛹 Deska (Plank)"),
        ExerciseCategory.LegRaise => ("Brzuch", "🧗 Unoszenie nóg w zwisie"),
        ExerciseCategory.Crunch or ExerciseCategory.SitUp => ("Brzuch", "⌚ Brzuszki (Garmin)"),
        _ => ("Inne", "⌚ Ćwiczenie z Garmina")
    };

    private static DateTime? ToLocal(Dynastream.Fit.DateTime? fitDate) =>
        fitDate?.GetDateTime().ToLocalTime();
}

public class FitImportResult
{
    public int Runs { get; set; }
    public int GymExercises { get; set; }
    public int Duplicates { get; set; }
    public List<string> Unsupported { get; } = new();
    public List<string> Errors { get; } = new();

    public string ToMessage()
    {
        var lines = new List<string>
        {
            $"Biegi: {Runs}",
            $"Ćwiczenia siłowe: {GymExercises}"
        };
        if (Duplicates > 0) lines.Add($"Pominięte (już w bazie): {Duplicates}");
        if (Unsupported.Count > 0) lines.Add($"Pominięte (inny sport niż bieg/siłownia): {Unsupported.Count}");
        if (Errors.Count > 0) lines.Add($"Błędy:\n{string.Join("\n", Errors)}");
        return string.Join("\n", lines);
    }
}
