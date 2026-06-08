using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessTracker.Models;
using FitnessTracker.Services;
using FitnessTracker.Views;

namespace FitnessTracker.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private readonly ApiService _apiService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotSyncing))]
    [NotifyPropertyChangedFor(nameof(SyncButtonText))]
    private bool isSyncing;

    public bool IsNotSyncing => !IsSyncing;
    public string SyncButtonText => IsSyncing ? "Pobieranie danych..." : "Pobierz najnowszy bieg";

    public DashboardViewModel(DatabaseService databaseService, ApiService apiService)
    {
        _databaseService = databaseService;
        _apiService = apiService;
    }
    

    [RelayCommand]
    private async Task GoToStatsAsync() => await Shell.Current.GoToAsync("StatsPage");
    [RelayCommand]
    private async Task GoToGymAsync() => await Shell.Current.GoToAsync("GymWorkoutPage");

    [RelayCommand]
    private async Task GoToRunAsync() => await Shell.Current.GoToAsync("RunWorkoutPage");

    [RelayCommand]
    private async Task GoToHistoryAsync() => await Shell.Current.GoToAsync("HistoryPage");

    [RelayCommand]
    private async Task GoToRecordsAsync()
    {
        await Shell.Current.GoToAsync("records");
    }
    [RelayCommand]
    private async Task SyncApiWorkoutsAsync()
    {
        if (IsSyncing)
            return;

        IsSyncing = true;

        try
        {
            // 1. Pobieramy bieg ze Stravy
            var latestRun = await _apiService.GetLatestRunAsWorkoutAsync();

            if (latestRun == null)
            {
                if (App.Current?.MainPage != null)
                    await App.Current.MainPage.DisplayAlert("Info", "Nie znaleziono żadnego biegu na Stravie.", "OK");
                return;
            }

            // 2. Sprawdzamy, czy ten bieg już jest w bazie
            if (latestRun.StravaActivityId.HasValue)
            {
                bool alreadyExists = await _databaseService.ExistsByStravaActivityIdAsync(latestRun.StravaActivityId.Value);

                if (alreadyExists)
                {
                    if (App.Current?.MainPage != null)
                        await App.Current.MainPage.DisplayAlert("Info", "Ten bieg jest już zapisany w bazie.", "OK");
                    return;
                }
            }

            // 3. Pokazujemy POPUP z uzupełnionymi danymi zamiast cichego zapisu
            if (Shell.Current?.CurrentPage != null)
            {
                var popup = new EditRunWorkoutPopup(latestRun, async finalWorkout =>
                {
                    await _databaseService.SaveWorkoutAsync(finalWorkout);

                    if (App.Current?.MainPage != null)
                    {
                        await App.Current.MainPage.DisplayAlert(
                            "Sukces",
                            $"Zapisano bieg ze Stravy:\n{finalWorkout.Date:yyyy-MM-dd HH:mm}\nDystans: {finalWorkout.Distance} km\nCzas: {finalWorkout.Duration} min",
                            "OK");
                    }

                    // Jeśli jesteś na ekranie z listą, możesz tu też odświeżyć dane:
                    // await LoadDataAsync();
                });

                await Shell.Current.CurrentPage.ShowPopupAsync(popup);
            }
        }
        catch (Exception ex)
        {
            if (App.Current?.MainPage != null)
            {
                await App.Current.MainPage.DisplayAlert("Błąd synchronizacji", ex.Message, "OK");
            }
        }
        finally
        {
            IsSyncing = false;
        }
    }
}