using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessTracker.Services;
using FitnessTracker.Views;
using System.Globalization;

namespace FitnessTracker.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private static readonly CultureInfo Pl = new("pl-PL");
    private readonly DatabaseService _databaseService;

    [ObservableProperty]
    private string greeting = "Cześć!";

    [ObservableProperty]
    private string todayText = string.Empty;

    [ObservableProperty]
    private string weekCountText = "0";

    [ObservableProperty]
    private string streakText = "0 tyg.";

    [ObservableProperty]
    private string lastWorkoutText = "—";

    [ObservableProperty]
    private string lastWorkoutDetail = "Brak treningów";

    [ObservableProperty]
    private string themeIcon = "";

    public DashboardViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        UpdateThemeIcon();
    }

    // =========================================================
    // PODSUMOWANIE
    // =========================================================

    [RelayCommand]
    public async Task LoadSummaryAsync()
    {
        var now = DateTime.Now;
        Greeting = now.Hour switch
        {
            < 5 => "Dobry wieczór",
            < 12 => "Dzień dobry",
            < 18 => "Cześć!",
            _ => "Dobry wieczór"
        };
        // Tylko pierwsza litera wielka: "Poniedziałek, 16 listopada"
        var today = now.ToString("dddd, d MMMM", Pl);
        TodayText = char.ToUpper(today[0], Pl) + today[1..];

        try
        {
            var workouts = await _databaseService.GetWorkoutsAsync();
            var trainingDays = workouts.Select(w => w.Date.Date).Distinct().ToList();

            var weekStart = StartOfWeek(now);
            WeekCountText = trainingDays.Count(d => d >= weekStart).ToString(Pl);

            StreakText = $"{WeekStreak(trainingDays, weekStart)} tyg.";

            var last = workouts.OrderByDescending(w => w.Date).FirstOrDefault();
            if (last == null)
            {
                LastWorkoutText = "—";
                LastWorkoutDetail = "Brak treningów";
            }
            else
            {
                var what = last.Type == "Bieganie"
                    ? $"Bieg {last.Distance:0.#} km"
                    : string.IsNullOrWhiteSpace(last.Category) ? "Siłownia" : last.Category;

                // Krótka wartość mieści się w kafelku: "2 dni" + "temu · Ramiona"
                var days = (now.Date - last.Date.Date).Days;
                (LastWorkoutText, LastWorkoutDetail) = days switch
                {
                    0 => ("Dziś", what),
                    1 => ("Wczoraj", what),
                    < 7 => ($"{days} dni", $"temu · {what}"),
                    _ => (last.Date.ToString("d MMM", Pl), what)
                };
            }
        }
        catch
        {
            // Podsumowanie jest dodatkiem – błąd bazy nie blokuje ekranu głównego
        }
    }

    /// <summary>Ile kolejnych tygodni (licząc od bieżącego lub poprzedniego) miało choć jeden trening.</summary>
    private static int WeekStreak(IReadOnlyCollection<DateTime> days, DateTime currentWeekStart)
    {
        var weeks = days.Select(StartOfWeek).ToHashSet();
        var week = weeks.Contains(currentWeekStart) ? currentWeekStart : currentWeekStart.AddDays(-7);

        var streak = 0;
        while (weeks.Contains(week))
        {
            streak++;
            week = week.AddDays(-7);
        }

        return streak;
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }

    // =========================================================
    // MOTYW
    // =========================================================

    [RelayCommand]
    private async Task ToggleThemeAsync()
    {
        ThemeService.Mode = ThemeService.Next();
        UpdateThemeIcon();

        var label = ThemeService.Mode switch
        {
            ThemeMode.Light => "Motyw jasny",
            ThemeMode.Dark => "Motyw ciemny",
            _ => "Motyw systemowy"
        };

        try
        {
            await CommunityToolkit.Maui.Alerts.Toast.Make(label).Show();
        }
        catch
        {
            // Toast nie jest dostępny na każdej platformie
        }
    }

    private void UpdateThemeIcon() => ThemeIcon = ThemeService.Mode switch
    {
        ThemeMode.Light => "", // light_mode
        ThemeMode.Dark => "",  // dark_mode
        _ => ""                // brightness_auto
    };

    // =========================================================
    // NAWIGACJA
    // =========================================================

    [RelayCommand]
    private async Task GoToWorkoutTemplatesAsync() => await Shell.Current.GoToAsync("WorkoutTemplatesPage");

    [RelayCommand]
    private async Task GoToManageExercisesAsync() => await Shell.Current.GoToAsync("ManageExPage");

    [RelayCommand]
    private async Task GoToStatsAsync() => await Shell.Current.GoToAsync("StatsPage");

    [RelayCommand]
    private async Task GoToProgressAsync() => await Shell.Current.GoToAsync(nameof(ProgressPage));

    [RelayCommand]
    private async Task GoToDataSyncAsync() => await Shell.Current.GoToAsync(nameof(DataSyncPage));

    [RelayCommand]
    private async Task GoToGymAsync() => await Shell.Current.GoToAsync("GymWorkoutPage");

    [RelayCommand]
    private async Task GoToRunAsync() => await Shell.Current.GoToAsync("RunWorkoutPage");

    [RelayCommand]
    private async Task GoToHistoryAsync() => await Shell.Current.GoToAsync("HistoryPage");

    [RelayCommand]
    private async Task GoToRecordsAsync() => await Shell.Current.GoToAsync("records");
}
