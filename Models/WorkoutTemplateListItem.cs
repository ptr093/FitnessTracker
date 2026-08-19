namespace FitnessTracker.Models;

public class WorkoutTemplateListItem
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int ExerciseCount { get; set; }

    public string Subtitle =>
        ExerciseCount switch
        {
            0 => "Brak ćwiczeń",
            1 => "1 ćwiczenie",
            _ => $"{ExerciseCount} ćwiczenia"
        };
}