namespace FitnessTracker.Models;

public class WorkoutTemplateItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ExerciseCount { get; set; }

    public string Subtitle =>
        ExerciseCount == 1
            ? "1 ćwiczenie"
            : $"{ExerciseCount} ćwiczenia";
}