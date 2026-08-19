using CommunityToolkit.Mvvm.ComponentModel;

namespace FitnessTracker.Models;

public partial class TemplateExerciseEditorItem : ObservableObject
{
    [ObservableProperty]
    private string category = string.Empty;

    [ObservableProperty]
    private string exercise = string.Empty;

    [ObservableProperty]
    private int setCount = 3;

    public int SortOrder { get; set; }
}
