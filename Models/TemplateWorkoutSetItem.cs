using CommunityToolkit.Mvvm.ComponentModel;

namespace FitnessTracker.Models;

public partial class TemplateWorkoutSetItem : ObservableObject
{
    public TemplateWorkoutExerciseItem ParentExercise { get; set; } = null!;

    [ObservableProperty]
    private int setNumber;

    [ObservableProperty]
    private double weight;

    [ObservableProperty]
    private int reps;
}