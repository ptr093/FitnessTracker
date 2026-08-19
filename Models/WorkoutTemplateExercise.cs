using SQLite;

namespace FitnessTracker.Models;

[Table("WorkoutTemplateExercises")]
public class WorkoutTemplateExercise
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int TemplateId { get; set; }

    [NotNull]
    public string Category { get; set; } = string.Empty;

    [NotNull]
    public string Exercise { get; set; } = string.Empty;

    // Kolejność wyświetlania ćwiczeń w treningu.
    public int SortOrder { get; set; }

    // Ile pustych serii utworzyć, gdy nie ma historii ćwiczenia.
    public int DefaultSetCount { get; set; } = 3;

    // Opcjonalny domyślny ciężar. Gdy brak historii, możesz go użyć.
    public double DefaultWeight { get; set; }

    // Opcjonalna liczba powtórzeń.
    public int DefaultReps { get; set; }
}