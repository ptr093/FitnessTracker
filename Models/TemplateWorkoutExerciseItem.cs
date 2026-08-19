using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace FitnessTracker.Models;

public partial class TemplateWorkoutExerciseItem : ObservableObject
{
    public string Category { get; set; } = string.Empty;

    public string Exercise { get; set; } = string.Empty;

    public ObservableCollection<TemplateWorkoutSetItem> Sets { get; } = new();
}