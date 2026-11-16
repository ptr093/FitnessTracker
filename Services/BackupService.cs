using CommunityToolkit.Maui.Storage;
using FitnessTracker.Models;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FitnessTracker.Services;

/// <summary>
/// Eksport / import wszystkich danych aplikacji (JSON) oraz eksport serii do CSV.
/// </summary>
public class BackupService
{
    private const int CurrentVersion = 1;
    private const string CustomExercisesKey = "CustomUserExercisesDB";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly DatabaseService _databaseService;
    private readonly ExerciseCatalogService _catalogService;

    public BackupService(DatabaseService databaseService, ExerciseCatalogService catalogService)
    {
        _databaseService = databaseService;
        _catalogService = catalogService;
    }

    public async Task<string?> ExportJsonAsync(CancellationToken token = default)
    {
        var backup = new BackupFile
        {
            Version = CurrentVersion,
            CreatedAt = DateTime.Now,
            Workouts = await _databaseService.GetWorkoutsAsync(),
            Templates = await _databaseService.GetWorkoutTemplatesAsync(),
            TemplateExercises = await _databaseService.GetAllTemplateExercisesAsync(),
            CustomExercises = _catalogService.GetCustomExercises()
        };

        var json = JsonSerializer.Serialize(backup, JsonOptions);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await FileSaver.Default.SaveAsync(
            $"fitnesstracker-backup-{DateTime.Now:yyyy-MM-dd}.json", stream, token);

        if (!result.IsSuccessful && result.Exception is not null and not OperationCanceledException)
            throw result.Exception;

        return result.IsSuccessful ? result.FilePath : null;
    }

    public async Task<string?> ExportCsvAsync(CancellationToken token = default)
    {
        var workouts = await _databaseService.GetWorkoutsAsync();
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;

        // Średnik jako separator – Excel w polskiej wersji otwiera to poprawnie
        sb.AppendLine("Data;Typ;Partia;Cwiczenie;Seria;Ciezar_kg;Powtorzenia;Szac_1RM;Dystans_km;Czas_min;Lokalizacja");

        foreach (var w in workouts.OrderBy(w => w.Date))
        {
            var date = w.Date.ToString("yyyy-MM-dd HH:mm", inv);

            if (w.Type == "Siłownia")
            {
                foreach (var set in DatabaseService.GetOrderedSets(w))
                {
                    var e1rm = ProgressionCalculator.EstimateOneRepMax(set.Weight, set.Reps);
                    sb.AppendLine(string.Join(';',
                        date, w.Type, Csv(w.Category), Csv(w.Exercise), set.SetNumber,
                        set.Weight.ToString("0.##", inv), set.Reps, e1rm.ToString("0.#", inv),
                        "", "", Csv(w.Location)));
                }
            }
            else
            {
                sb.AppendLine(string.Join(';',
                    date, w.Type, "", Csv(w.Exercise), "", "", "", "",
                    w.Distance.ToString("0.##", inv), w.Duration.ToString("0.#", inv), Csv(w.Location)));
            }
        }

        // BOM, żeby Excel poprawnie odczytał polskie znaki
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        using var stream = new MemoryStream(bytes);

        var result = await FileSaver.Default.SaveAsync(
            $"fitnesstracker-{DateTime.Now:yyyy-MM-dd}.csv", stream, token);

        if (!result.IsSuccessful && result.Exception is not null and not OperationCanceledException)
            throw result.Exception;

        return result.IsSuccessful ? result.FilePath : null;
    }

    public async Task<BackupImportResult?> ImportJsonAsync()
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Wybierz plik kopii zapasowej (.json)",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.Android] = new[] { "application/json", "text/plain", "application/octet-stream" },
                [DevicePlatform.iOS] = new[] { "public.json", "public.text" },
                [DevicePlatform.MacCatalyst] = new[] { "public.json", "public.text" },
                [DevicePlatform.WinUI] = new[] { ".json" }
            })
        });

        if (file == null)
            return null;

        await using var stream = await file.OpenReadAsync();
        var backup = await JsonSerializer.DeserializeAsync<BackupFile>(stream, JsonOptions)
                     ?? throw new InvalidDataException("Plik nie jest kopią zapasową FitnessTracker.");

        if (backup.Version > CurrentVersion)
            throw new InvalidDataException("Kopia pochodzi z nowszej wersji aplikacji.");

        var (workouts, templates) = await _databaseService.ImportBackupAsync(
            backup.Workouts, backup.Templates, backup.TemplateExercises);

        var customAdded = 0;
        var existingCustom = _catalogService.GetCustomExercises();
        foreach (var (category, exercises) in backup.CustomExercises)
        {
            existingCustom.TryGetValue(category, out var current);
            foreach (var exercise in exercises)
            {
                if (current?.Contains(exercise) == true)
                    continue;

                _catalogService.AddCustomExercise(category, exercise);
                customAdded++;
            }
        }

        return new BackupImportResult(workouts, backup.Workouts.Count - workouts, templates, customAdded);
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Contains(';') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}

public class BackupFile
{
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<Workout> Workouts { get; set; } = new();
    public List<WorkoutTemplate> Templates { get; set; } = new();
    public List<WorkoutTemplateExercise> TemplateExercises { get; set; } = new();
    public Dictionary<string, List<string>> CustomExercises { get; set; } = new();
}

public record BackupImportResult(int WorkoutsAdded, int WorkoutsSkipped, int TemplatesAdded, int CustomExercisesAdded);
