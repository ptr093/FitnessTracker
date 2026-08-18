using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace FitnessTracker.ViewModels;

public partial class ManageExercisesViewModel : ObservableObject
{
    private const string CustomExercisesKey = "CustomUserExercisesDB";
    private const string CustomExercisePrefix = "⭐ ";

    private Dictionary<string, List<string>> _customUserExercises = new();

    public ObservableCollection<string> Categories { get; } = new();

    public ObservableCollection<ExerciseItem> VisibleExercises { get; } = new();

    [ObservableProperty]
    private string? selectedCategory;

    public ManageExercisesViewModel()
    {
        LoadCustomExercises();
    }

    private void LoadCustomExercises()
    {
        var previouslySelectedCategory = SelectedCategory;

        _customUserExercises = ReadCustomExercises();

        Categories.Clear();

        // Wyświetl tylko partie, w których użytkownik ma własne ćwiczenia.
        foreach (var category in _customUserExercises
                     .Where(x => x.Value.Count > 0)
                     .Select(x => x.Key)
                     .OrderBy(x => x))
        {
            Categories.Add(category);
        }

        SelectedCategory = !string.IsNullOrWhiteSpace(previouslySelectedCategory)
                           && Categories.Contains(previouslySelectedCategory)
            ? previouslySelectedCategory
            : Categories.FirstOrDefault();

        RefreshVisibleExercises();
    }

    private static Dictionary<string, List<string>> ReadCustomExercises()
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

    partial void OnSelectedCategoryChanged(string? value)
    {
        RefreshVisibleExercises();
    }

    private void RefreshVisibleExercises()
    {
        VisibleExercises.Clear();

        if (string.IsNullOrWhiteSpace(SelectedCategory))
            return;

        if (!_customUserExercises.TryGetValue(SelectedCategory, out var exercises))
            return;

        foreach (var exerciseName in exercises)
        {
            VisibleExercises.Add(new ExerciseItem
            {
                Category = SelectedCategory,
                Name = exerciseName
            });
        }
    }

    [RelayCommand]
    private async Task EditExerciseAsync(ExerciseItem? exercise)
    {
        if (exercise is null || App.Current?.MainPage is null)
            return;

        var nameForEditing = RemoveCustomPrefix(exercise.Name);

        var enteredName = await App.Current.MainPage.DisplayPromptAsync(
            "Edytuj ćwiczenie",
            "Nowa nazwa ćwiczenia:",
            "Zapisz",
            "Anuluj",
            initialValue: nameForEditing);

        if (string.IsNullOrWhiteSpace(enteredName))
            return;

        var cleanName = RemoveCustomPrefix(enteredName.Trim());

        if (string.IsNullOrWhiteSpace(cleanName))
            return;

        var newExerciseName = $"{CustomExercisePrefix}{cleanName}";

        if (string.Equals(newExerciseName, exercise.Name, StringComparison.Ordinal))
            return;

        if (!_customUserExercises.TryGetValue(exercise.Category, out var exercises))
            return;

        var nameAlreadyExists = exercises.Any(x =>
            string.Equals(x, newExerciseName, StringComparison.OrdinalIgnoreCase));

        if (nameAlreadyExists)
        {
            await App.Current.MainPage.DisplayAlert(
                "Edycja ćwiczenia",
                "Ćwiczenie o tej nazwie już istnieje w tej partii.",
                "OK");

            return;
        }

        var index = exercises.FindIndex(x =>
            string.Equals(x, exercise.Name, StringComparison.Ordinal));

        if (index < 0)
            return;

        exercises[index] = newExerciseName;

        SaveCustomExercises();
        LoadCustomExercises();
    }

    [RelayCommand]
    private async Task DeleteExerciseAsync(ExerciseItem? exercise)
    {
        if (exercise is null || App.Current?.MainPage is null)
            return;

        var confirmed = await App.Current.MainPage.DisplayAlert(
            "Usuń ćwiczenie",
            $"Czy na pewno chcesz usunąć ćwiczenie „{exercise.Name}”?\n\n" +
            "Zostanie ono usunięte z listy wyboru. " +
            "Wcześniej zapisane treningi pozostaną w historii.",
            "Usuń",
            "Anuluj");

        if (!confirmed)
            return;

        if (!_customUserExercises.TryGetValue(exercise.Category, out var exercises))
            return;

        exercises.Remove(exercise.Name);

        if (exercises.Count == 0)
        {
            _customUserExercises.Remove(exercise.Category);
        }

        SaveCustomExercises();
        LoadCustomExercises();
    }

    private void SaveCustomExercises()
    {
        var json = JsonSerializer.Serialize(_customUserExercises);
        Preferences.Default.Set(CustomExercisesKey, json);
    }

    private static string RemoveCustomPrefix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value
            .Trim()
            .TrimStart('⭐')
            .Trim();
    }
}

public class ExerciseItem
{
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}