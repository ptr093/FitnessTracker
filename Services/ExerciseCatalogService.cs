using Microsoft.Maui.Storage;
using System.Text.Json;

namespace FitnessTracker.Services;

public class ExerciseCatalogService
{
    private const string CustomExercisesKey = "CustomUserExercisesDB";
    private const string AddCustomExerciseItem = "➕ Dodaj własne ćwiczenie...";
    private const string CustomExercisePrefix = "⭐ ";

    public Dictionary<string, List<string>> GetAllExercises()
    {
        var database = CreateDefaultDatabase();
        var customExercises = GetCustomExercises();

        foreach (var category in customExercises)
        {
            if (!database.TryGetValue(category.Key, out var exercises))
                continue;

            foreach (var customExercise in category.Value)
            {
                if (!exercises.Contains(customExercise))
                {
                    exercises.Add(customExercise);
                }
            }
        }

        return database;
    }

    public Dictionary<string, List<string>> GetCustomExercises()
    {
        var json = Preferences.Default.Get(CustomExercisesKey, string.Empty);

        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, List<string>>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json)
                   ?? new Dictionary<string, List<string>>();
        }
        catch
        {
            return new Dictionary<string, List<string>>();
        }
    }

    public void AddCustomExercise(string category, string enteredName)
    {
        if (string.IsNullOrWhiteSpace(category))
            return;

        var cleanName = enteredName
            .Trim()
            .TrimStart('⭐')
            .Trim();

        if (string.IsNullOrWhiteSpace(cleanName))
            return;

        var exerciseName = $"{CustomExercisePrefix}{cleanName}";
        var customExercises = GetCustomExercises();

        if (!customExercises.TryGetValue(category, out var exercises))
        {
            exercises = new List<string>();
            customExercises[category] = exercises;
        }

        if (!exercises.Any(x =>
                string.Equals(x, exerciseName, StringComparison.OrdinalIgnoreCase)))
        {
            exercises.Add(exerciseName);
        }

        Preferences.Default.Set(
            CustomExercisesKey,
            JsonSerializer.Serialize(customExercises));
    }

    public static bool IsAddCustomExerciseItem(string? item)
    {
        return item == AddCustomExerciseItem;
    }

    public string GetAddCustomExerciseItem() => AddCustomExerciseItem;

    private static Dictionary<string, List<string>> CreateDefaultDatabase()
    {
        return new Dictionary<string, List<string>>
        {
            ["Klatka piersiowa"] = new()
            {
                "🏋️ Wyciskanie sztangi leżąc",
                "🏋️ Wyciskanie sztangi skos górny",
                "🔩 Wyciskanie hantli leżąc",
                "🔩 Wyciskanie hantli skos",
                "🤖 Hammer (Wyciskanie maszyna)",
                "🦋 Rozpiętki hantlami",
                "🦾 Pompki na poręczach (Dips)"
            },

            ["Plecy"] = new()
            {
                "🦅 Ściąganie drążka (Lat Pulldown)",
                "🦅 Ściąganie chwytu wyciągu górnego po skosie",
                "🚣 Wiosłowanie sztangą",
                "🚣 Przyciąganie linki wyciągu dolnego siedząc",
                "🧗 Podciąganie na drążku",
                "🔥 Martwy ciąg"
            },

            ["Nogi"] = new()
            {
                "🦵 Przysiad ze sztangą (Squat)",
                "🏗️ Wyciskanie na suwnicy",
                "🍑 Hip Thrust (Unoszenie bioder)",
                "🚶 Wykroki",
                "🦵 Uginanie nóg na maszynie",
                "🦵 Wznosy na łydki"
            },

            ["Barki"] = new()
            {
                "🛡️ Wyciskanie żołnierskie (OHP)",
                "🔩 Wyciskanie hantli siedząc",
                "🦅 Wznosy bokiem",
                "🦅 Wznosy w opadzie",
                "🦋 Odwodzenie ramion na maszynie Butterfly odwrotnie (Rear Delt Fly)",
                "🎯 Face pulls",
                "⬆️ Unoszenie hantli w przód"
            },

            ["Ramiona"] = new()
            {
                "💪 Uginanie ze sztangą (Biceps)",
                "🔨 Uginanie młotkowe",
                "💪 Wyciskanie francuskie",
                "🪢 Prostowanie z linką wyciągu"
            },

            ["Brzuch"] = new()
            {
                "🍫 Allahy (Crunches z linką)",
                "🚲 Rowerek",
                "🛹 Deska (Plank)",
                "🧗 Unoszenie nóg w zwisie"
            }
        };
    }
}