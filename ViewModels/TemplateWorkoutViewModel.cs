using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessTracker.Models;
using FitnessTracker.Services;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace FitnessTracker.ViewModels;

public partial class TemplateWorkoutViewModel : ObservableObject, IQueryAttributable
{
    private readonly DatabaseService _databaseService;
    private bool _hasLoaded;

    public ObservableCollection<TemplateWorkoutExerciseItem> Exercises { get; } = new();

    [ObservableProperty]
    private string templateName = "Trening z szablonu";

    [ObservableProperty]
    private DateTime workoutDate = DateTime.Now;

    [ObservableProperty]
    private string? location;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isSaving;

    [ObservableProperty]
    private bool isExercisesEmpty;

    public TemplateWorkoutViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async void ApplyQueryAttributes(
     IDictionary<string, object> query)
    {
        try
        {
            if (_hasLoaded)
                return;

            if (!query.TryGetValue("templateId", out var rawTemplateId) ||
                !int.TryParse(rawTemplateId?.ToString(), out var templateId))
            {
                await Shell.Current.GoToAsync("..");
                return;
            }

            _hasLoaded = true;

            await LoadTemplateAsync(templateId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"TemplateWorkoutPage crash: {ex}");

            if (App.Current?.MainPage is not null)
            {
                await App.Current.MainPage.DisplayAlert(
                    "Błąd uruchamiania szablonu",
                    $"Nie udało się otworzyć treningu.\n\n{ex.Message}",
                    "OK");
            }

            await Shell.Current.GoToAsync("..");
        }
    }

    private async Task LoadTemplateAsync(int templateId)
    {
        try
        {
            IsLoading = true;
            WorkoutDate = DateTime.Now;
            Exercises.Clear();
            IsExercisesEmpty = false;

            var template = await _databaseService.GetWorkoutTemplateAsync(templateId);
            if (template is null)
            {
                await Shell.Current.GoToAsync("..");
                return;
            }

            TemplateName = template.Name;

            var templateExercises = await _databaseService.GetTemplateExercisesAsync(templateId);

            foreach (var templateExercise in templateExercises.OrderBy(x => x.SortOrder))
            {
                var exerciseItem = new TemplateWorkoutExerciseItem
                {
                    Category = templateExercise.Category,
                    Exercise = templateExercise.Exercise
                };

                var lastSets = await _databaseService
                    .GetLastGymSetsForExerciseAsync(templateExercise.Exercise);

                if (lastSets.Count > 0)
                {
                    foreach (var previousSet in lastSets.OrderBy(x => x.SetNumber))
                    {
                        exerciseItem.Sets.Add(new TemplateWorkoutSetItem
                        {
                            ParentExercise = exerciseItem,
                            SetNumber = exerciseItem.Sets.Count + 1,
                            Weight = previousSet.Weight,
                            Reps = previousSet.Reps
                        });
                    }
                }
                else
                {
                    for (var number = 1; number <= Math.Max(1, templateExercise.DefaultSetCount); number++)
                    {
                        exerciseItem.Sets.Add(new TemplateWorkoutSetItem
                        {
                            ParentExercise = exerciseItem,
                            SetNumber = number,
                            Weight = 0,
                            Reps = 0
                        });
                    }
                }

                Exercises.Add(exerciseItem);
            }

            IsExercisesEmpty = Exercises.Count == 0;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void AddSet(TemplateWorkoutExerciseItem? exercise)
    {
        if (exercise is null)
            return;

        exercise.Sets.Add(new TemplateWorkoutSetItem
        {
            ParentExercise = exercise,
            SetNumber = exercise.Sets.Count + 1,
            Weight = 0,
            Reps = 0
        });
    }

    [RelayCommand]
    private void RemoveSet(TemplateWorkoutSetItem? set)
    {
        if (set?.ParentExercise is null)
            return;

        var exercise = set.ParentExercise;

        if (exercise.Sets.Remove(set))
        {
            RenumberSets(exercise);
        }
    }

    [RelayCommand]
    private async Task RemoveExerciseAsync(TemplateWorkoutExerciseItem? exercise)
    {
        if (exercise is null || App.Current?.MainPage is null)
            return;

        var confirmed = await App.Current.MainPage.DisplayAlert(
            "Pomiń ćwiczenie",
            $"Czy na pewno chcesz pominąć ćwiczenie „{exercise.Exercise}” w tym treningu?\n\nSzablon pozostanie bez zmian.",
            "Pomiń",
            "Anuluj");

        if (!confirmed)
            return;

        Exercises.Remove(exercise);
        IsExercisesEmpty = Exercises.Count == 0;
    }

    [RelayCommand]
    private async Task SaveWorkoutAsync()
    {
        if (IsSaving)
            return;

        if (Exercises.Count == 0)
        {
            await ShowAlertAsync("Nie ma żadnego ćwiczenia do zapisania.");
            return;
        }

        try
        {
            IsSaving = true;

            var workoutsToSave = new List<Workout>();

            foreach (var exerciseItem in Exercises)
            {
                var validSets = exerciseItem.Sets
                    .Where(x => x.Weight > 0 || x.Reps > 0)
                    .Select((set, index) => new WorkoutSet
                    {
                        SetNumber = index + 1,
                        Weight = set.Weight,
                        Reps = set.Reps
                    })
                    .ToList();

                if (validSets.Count == 0)
                    continue;

                workoutsToSave.Add(new Workout
                {
                    Type = "Siłownia",
                    Date = WorkoutDate,
                    Location = string.IsNullOrWhiteSpace(Location)
                        ? null
                        : Location.Trim(),
                    Category = exerciseItem.Category,
                    Exercise = exerciseItem.Exercise,
                    GymSetsJson = JsonSerializer.Serialize(validSets)
                });
            }

            if (workoutsToSave.Count == 0)
            {
                await ShowAlertAsync("Uzupełnij przynajmniej jedną serię.");
                return;
            }

            foreach (var workout in workoutsToSave)
            {
                await _databaseService.SaveWorkoutAsync(workout);
            }

            if (App.Current?.MainPage is not null)
            {
                await App.Current.MainPage.DisplayAlert(
                    "Sukces",
                    $"Zapisano trening: {workoutsToSave.Count} ćwiczeń.",
                    "OK");
            }

            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    private static void RenumberSets(TemplateWorkoutExerciseItem exercise)
    {
        for (var index = 0; index < exercise.Sets.Count; index++)
        {
            exercise.Sets[index].SetNumber = index + 1;
        }
    }

    private static async Task ShowAlertAsync(string message)
    {
        if (App.Current?.MainPage is not null)
        {
            await App.Current.MainPage.DisplayAlert(
                "Trening z szablonu",
                message,
                "OK");
        }
    }
}

