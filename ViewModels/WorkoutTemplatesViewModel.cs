using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessTracker.Models;
using FitnessTracker.Services;
using FitnessTracker.Views;
using System.Collections.ObjectModel;

namespace FitnessTracker.ViewModels;

public partial class WorkoutTemplatesViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;

    private bool _isLoading;

    public ObservableCollection<WorkoutTemplateListItem> Templates { get; } = new();

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private bool showEmptyState;

    public WorkoutTemplatesViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_isLoading)
            return;

        try
        {
            _isLoading = true;
            IsRefreshing = true;

            var templates = await _databaseService.GetWorkoutTemplatesAsync();

            Templates.Clear();

            foreach (var template in templates)
            {
                var exercises = await _databaseService
                    .GetTemplateExercisesAsync(template.Id);

                Templates.Add(new WorkoutTemplateListItem
                {
                    Id = template.Id,
                    Name = template.Name,
                    Description = template.Description,
                    ExerciseCount = exercises.Count
                });
            }

            ShowEmptyState = Templates.Count == 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"BŁĄD ładowania szablonów: {ex}");

            if (App.Current?.MainPage is not null)
            {
                await App.Current.MainPage.DisplayAlert(
                    "Błąd szablonów",
                    $"Nie udało się wczytać szablonów.\n\n{ex.Message}",
                    "OK");
            }

            // Pozwala stronie pozostać otwartą zamiast crashować.
            Templates.Clear();
            ShowEmptyState = true;
        }
        finally
        {
            IsRefreshing = false;
            _isLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddTemplateAsync()
    {
        await Shell.Current.GoToAsync(nameof(EditWorkoutTemplatePage));
    }

    [RelayCommand]
    private async Task EditTemplateAsync(WorkoutTemplateListItem? template)
    {
        if (template is null)
            return;

        await Shell.Current.GoToAsync(
            $"{nameof(EditWorkoutTemplatePage)}?templateId={template.Id}");
    }

    [RelayCommand]
    private async Task StartTemplateAsync(WorkoutTemplateListItem? template)
    {
        if (template is null)
            return;

        await Shell.Current.GoToAsync(
            $"{nameof(TemplateWorkoutPage)}?templateId={template.Id}");
    }

    [RelayCommand]
    private async Task DeleteTemplateAsync(WorkoutTemplateListItem? template)
    {
        if (template is null || App.Current?.MainPage is null)
            return;

        var confirmed = await App.Current.MainPage.DisplayAlert(
            "Usuń szablon",
            $"Czy na pewno chcesz usunąć szablon „{template.Name}”?",
            "Usuń",
            "Anuluj");

        if (!confirmed)
            return;

        await _databaseService.DeleteWorkoutTemplateAsync(template.Id);

        await LoadAsync();
    }
}