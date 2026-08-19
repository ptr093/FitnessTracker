using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessTracker.Models;
using FitnessTracker.Services;
using System.Collections.ObjectModel;
using System.Web;


namespace FitnessTracker.ViewModels;

public partial class EditWorkoutTemplateViewModel :
    ObservableObject,
    IQueryAttributable
{
    private readonly DatabaseService _databaseService;
    private readonly ExerciseCatalogService _exerciseCatalogService;

    private readonly Dictionary<string, List<string>> _exerciseCatalog = new();

    private int _templateId;

    public ObservableCollection<string> Categories { get; } = new();

    public ObservableCollection<string> AvailableExercises { get; } = new();

    public ObservableCollection<TemplateExerciseEditorItem> Exercises { get; } = new();

    [ObservableProperty]
    private string templateName = string.Empty;

    [ObservableProperty]
    private string? description;

    [ObservableProperty]
    private string? selectedCategory;

    [ObservableProperty]
    private string? selectedExercise;

    [ObservableProperty]
    private int selectedSetCount = 3;

    [ObservableProperty]
    private bool isLoading;

    [RelayCommand]
    private void IncreaseSetCount()
    {
        if (SelectedSetCount < 12)
            SelectedSetCount++;
    }

    [RelayCommand]
    private void DecreaseSetCount()
    {
        if (SelectedSetCount > 1)
            SelectedSetCount--;
    }


    public string SaveButtonText => _templateId == 0
        ? "Utwórz szablon"
        : "Zapisz zmiany";

    public EditWorkoutTemplateViewModel(
        DatabaseService databaseService,
        ExerciseCatalogService exerciseCatalogService)
    {
        _databaseService = databaseService;
        _exerciseCatalogService = exerciseCatalogService;

        _exerciseCatalog = _exerciseCatalogService.GetAllExercises();

        foreach (var category in _exerciseCatalog.Keys)
        {
            Categories.Add(category);
        }
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("templateId", out var value))
        {
            PrepareNewTemplate();
            return;
        }

        if (!int.TryParse(value?.ToString(), out var templateId))
        {
            PrepareNewTemplate();
            return;
        }

        await LoadTemplateAsync(templateId);
    }

    private void PrepareNewTemplate()
    {
        _templateId = 0;
        TemplateName = string.Empty;
        Description = string.Empty;
        Exercises.Clear();

        SelectedCategory = Categories.FirstOrDefault();
        SelectedSetCount = 3;

        OnPropertyChanged(nameof(SaveButtonText));
    }

    partial void OnSelectedCategoryChanged(string? value)
    {
        AvailableExercises.Clear();
        SelectedExercise = null;

        if (string.IsNullOrWhiteSpace(value) ||
            !_exerciseCatalog.TryGetValue(value, out var exercises))
        {
            return;
        }

        foreach (var exercise in exercises)
        {
            AvailableExercises.Add(exercise);
        }

        SelectedExercise = AvailableExercises.FirstOrDefault();
    }

    private async Task LoadTemplateAsync(int templateId)
    {
        try
        {
            IsLoading = true;

            var template = await _databaseService.GetWorkoutTemplateAsync(templateId);

            if (template == null)
            {
                await Shell.Current.GoToAsync("..");
                return;
            }

            _templateId = template.Id;
            TemplateName = template.Name;
            Description = template.Description;

            Exercises.Clear();

            var templateExercises = await _databaseService
                .GetTemplateExercisesAsync(templateId);

            foreach (var item in templateExercises)
            {
                Exercises.Add(new TemplateExerciseEditorItem
                {
                    Category = item.Category,
                    Exercise = item.Exercise,
                    SetCount = item.DefaultSetCount,
                    SortOrder = item.SortOrder
                });
            }

            SelectedCategory = Categories.FirstOrDefault();
            OnPropertyChanged(nameof(SaveButtonText));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddExerciseAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedCategory))
        {
            await ShowAlertAsync("Wybierz partię mięśniową.");
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedExercise))
        {
            await ShowAlertAsync("Wybierz ćwiczenie.");
            return;
        }

        if (ExerciseCatalogService.IsAddCustomExerciseItem(SelectedExercise))
        {
            var enteredName = await App.Current!.MainPage!.DisplayPromptAsync(
                "Nowe ćwiczenie",
                "Podaj nazwę własnego ćwiczenia:",
                "Dodaj",
                "Anuluj",
                "np. Przyciąganie jednorącz");

            if (string.IsNullOrWhiteSpace(enteredName))
                return;

            _exerciseCatalogService.AddCustomExercise(
                SelectedCategory,
                enteredName);

            _exerciseCatalog.Clear();

            foreach (var item in _exerciseCatalogService.GetAllExercises())
            {
                _exerciseCatalog[item.Key] = item.Value;
            }

            OnSelectedCategoryChanged(SelectedCategory);

            var normalizedName = $"⭐ {enteredName.Trim().TrimStart('⭐').Trim()}";

            if (AvailableExercises.Contains(normalizedName))
            {
                SelectedExercise = normalizedName;
            }

            return;
        }

        var exists = Exercises.Any(x =>
            x.Category == SelectedCategory &&
            x.Exercise == SelectedExercise);

        if (exists)
        {
            await ShowAlertAsync("To ćwiczenie jest już w tym szablonie.");
            return;
        }

        Exercises.Add(new TemplateExerciseEditorItem
        {
            Category = SelectedCategory,
            Exercise = SelectedExercise,
            SetCount = Math.Max(1, SelectedSetCount),
            SortOrder = Exercises.Count
        });
    }

    [RelayCommand]
    private void RemoveExercise(TemplateExerciseEditorItem? item)
    {
        if (item is null)
            return;

        Exercises.Remove(item);
        RenumberExercises();
    }

    [RelayCommand]
    private void MoveExerciseUp(TemplateExerciseEditorItem? item)
    {
        if (item is null)
            return;

        var index = Exercises.IndexOf(item);

        if (index <= 0)
            return;

        Exercises.Move(index, index - 1);
        RenumberExercises();
    }

    [RelayCommand]
    private void MoveExerciseDown(TemplateExerciseEditorItem? item)
    {
        if (item is null)
            return;

        var index = Exercises.IndexOf(item);

        if (index < 0 || index >= Exercises.Count - 1)
            return;

        Exercises.Move(index, index + 1);
        RenumberExercises();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var name = TemplateName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            await ShowAlertAsync("Podaj nazwę szablonu, np. „Trening klaty”.");
            return;
        }

        if (Exercises.Count == 0)
        {
            await ShowAlertAsync("Dodaj przynajmniej jedno ćwiczenie.");
            return;
        }

        var template = new WorkoutTemplate
        {
            Id = _templateId,
            Name = name,
            Description = string.IsNullOrWhiteSpace(Description)
                ? null
                : Description.Trim()
        };

        var items = Exercises.Select((item, index) =>
            new WorkoutTemplateExercise
            {
                TemplateId = _templateId,
                Category = item.Category,
                Exercise = item.Exercise,
                DefaultSetCount = Math.Max(1, item.SetCount),
                SortOrder = index
            })
            .ToList();

        await _databaseService.SaveWorkoutTemplateAsync(template, items);
        await Shell.Current.GoToAsync("..");
    }



    private void RenumberExercises()
    {
        for (var i = 0; i < Exercises.Count; i++)
        {
            Exercises[i].SortOrder = i;
        }
    }

    private static async Task ShowAlertAsync(string message)
    {
        if (App.Current?.MainPage is not null)
        {
            await App.Current.MainPage.DisplayAlert(
                "Szablon treningu",
                message,
                "OK");
        }
    }
}