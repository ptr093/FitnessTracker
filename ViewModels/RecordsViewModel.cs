using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessTracker.Models;
using FitnessTracker.Services;
using Microsoft.Maui.Graphics;

namespace FitnessTracker.ViewModels;

public partial class RecordsViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private List<Workout> _allWorkouts = new();

    public ObservableCollection<RecordsCategoryItem> Categories { get; } = new();
    public ObservableCollection<RecordsOptionItem> StrengthCategories { get; } = new();
    public ObservableCollection<RecordsOptionItem> Exercises { get; } = new();
    public ObservableCollection<RecordItem> Records { get; } = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool showStrengthCategories;

    [ObservableProperty]
    private bool showExercises;

    [ObservableProperty]
    private bool showRecords;

    [ObservableProperty]
    private bool showEmptyState;

    [ObservableProperty]
    private string selectedHeader = "Wybierz kategorię rekordów";

    [ObservableProperty]
    private string selectedCategoryKey = string.Empty;

    [ObservableProperty]
    private string selectedStrengthCategory = string.Empty;

    [ObservableProperty]
    private string selectedExercise = string.Empty;

    public RecordsViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;

            _allWorkouts = await _databaseService.GetWorkoutsAsync();

            BuildMainCategories();
            ResetView();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectCategory(RecordsCategoryItem item)
    {
        if (item == null)
            return;

        SelectedCategoryKey = item.Key;
        SelectedStrengthCategory = string.Empty;
        SelectedExercise = string.Empty;

        Records.Clear();
        Exercises.Clear();
        StrengthCategories.Clear();

        if (item.Key == "run")
        {
            SelectedHeader = "Rekordy biegowe";
            BuildRunningRecords();
            ShowStrengthCategories = false;
            ShowExercises = false;
            ShowRecords = true;
            ShowEmptyState = Records.Count == 0;
        }
        else if (item.Key == "strength")
        {
            SelectedHeader = "Wybierz partię mięśniową";
            BuildStrengthCategories();
            ShowStrengthCategories = true;
            ShowExercises = false;
            ShowRecords = false;
            ShowEmptyState = StrengthCategories.Count == 0;
        }

        HighlightMainCategory(item.Key);
    }

    [RelayCommand]
    private void SelectStrengthCategory(RecordsOptionItem item)
    {
        if (item == null)
            return;

        SelectedStrengthCategory = item.Key;
        SelectedExercise = string.Empty;

        Exercises.Clear();
        Records.Clear();

        SelectedHeader = $"Ćwiczenia: {item.Title}";
        BuildExercises(item.Key);

        ShowStrengthCategories = true;
        ShowExercises = true;
        ShowRecords = false;
        ShowEmptyState = Exercises.Count == 0;

        HighlightStrengthCategory(item.Key);
    }

    [RelayCommand]
    private void SelectExercise(RecordsOptionItem item)
    {
        if (item == null)
            return;

        SelectedExercise = item.Key;
        Records.Clear();

        SelectedHeader = $"Rekordy: {item.Title}";
        BuildStrengthExerciseRecords(item.Key);

        ShowStrengthCategories = true;
        ShowExercises = true;
        ShowRecords = true;
        ShowEmptyState = Records.Count == 0;

        HighlightExercise(item.Key);
    }

    [RelayCommand]
    private void ResetSelection()
    {
        ResetView();
    }

    private void ResetView()
    {
        SelectedCategoryKey = string.Empty;
        SelectedStrengthCategory = string.Empty;
        SelectedExercise = string.Empty;

        SelectedHeader = "Wybierz kategorię rekordów";

        StrengthCategories.Clear();
        Exercises.Clear();
        Records.Clear();

        ShowStrengthCategories = false;
        ShowExercises = false;
        ShowRecords = false;
        ShowEmptyState = false;

        foreach (var item in Categories)
        {
            item.BackgroundColor = Color.FromArgb("#FFFFFF");
            item.BorderColor = Color.FromArgb("#E2E8F0");
            item.TextColor = Color.FromArgb("#1E293B");
        }

        OnPropertyChanged(nameof(Categories));
        OnPropertyChanged(nameof(StrengthCategories));
        OnPropertyChanged(nameof(Exercises));
    }

    private void BuildMainCategories()
    {
        Categories.Clear();

        Categories.Add(new RecordsCategoryItem
        {
            Key = "run",
            Title = "Bieganie",
            Subtitle = "Tempo, dystans i czas",
            Icon = "🏃",
            BackgroundColor = Color.FromArgb("#FFFFFF"),
            BorderColor = Color.FromArgb("#E2E8F0"),
            TextColor = Color.FromArgb("#1E293B")
        });

        Categories.Add(new RecordsCategoryItem
        {
            Key = "strength",
            Title = "Siłowe",
            Subtitle = "Partie, ćwiczenia i wyniki",
            Icon = "🏋️",
            BackgroundColor = Color.FromArgb("#FFFFFF"),
            BorderColor = Color.FromArgb("#E2E8F0"),
            TextColor = Color.FromArgb("#1E293B")
        });
    }

    private void BuildRunningRecords()
    {
        Records.Clear();

        var runs = _allWorkouts
            .Where(x => x.Type == "Bieganie")
            .OrderByDescending(x => x.Date)
            .ToList();

        if (!runs.Any())
            return;

        var longestRun = runs
            .OrderByDescending(x => x.Distance)
            .ThenBy(x => x.Duration)
            .FirstOrDefault();

        if (longestRun != null)
        {
            Records.Add(new RecordItem
            {
                Icon = "📏",
                Title = "Najdłuższy bieg",
                Subtitle = "Największy pokonany dystans",
                Category = "Bieganie",
                ValueText = $"{longestRun.Distance:0.##} km",
                DetailText = $"Czas: {longestRun.Duration:0.##} min",
                DateText = $"{longestRun.Date:dd.MM.yyyy}",
                ExtraText = longestRun.Distance > 0 && longestRun.Duration > 0
                    ? $"Tempo: {FormatPace(longestRun.Duration / longestRun.Distance)} min/km"
                    : "-",
                AccentBackground = Color.FromArgb("#DCFCE7"),
                BadgeBackground = Color.FromArgb("#DCFCE7"),
                BadgeTextColor = Color.FromArgb("#15803D")
            });
        }

        var fastestRun = runs
            .Where(x => x.Distance > 0 && x.Duration > 0)
            .OrderBy(x => x.Duration / x.Distance)
            .ThenByDescending(x => x.Distance)
            .FirstOrDefault();

        if (fastestRun != null)
        {
            Records.Add(new RecordItem
            {
                Icon = "⚡",
                Title = "Najlepsze tempo",
                Subtitle = "Najlepsze średnie tempo biegu",
                Category = "Bieganie",
                ValueText = $"{FormatPace(fastestRun.Duration / fastestRun.Distance)} min/km",
                DetailText = $"Dystans: {fastestRun.Distance:0.##} km",
                DateText = $"{fastestRun.Date:dd.MM.yyyy}",
                ExtraText = $"Czas: {fastestRun.Duration:0.##} min",
                AccentBackground = Color.FromArgb("#DBEAFE"),
                BadgeBackground = Color.FromArgb("#DBEAFE"),
                BadgeTextColor = Color.FromArgb("#1D4ED8")
            });
        }

        var longestDurationRun = runs
            .OrderByDescending(x => x.Duration)
            .ThenByDescending(x => x.Distance)
            .FirstOrDefault();

        if (longestDurationRun != null)
        {
            Records.Add(new RecordItem
            {
                Icon = "⏱️",
                Title = "Najdłuższy czas biegu",
                Subtitle = "Najdłuższa jednostka biegowa",
                Category = "Bieganie",
                ValueText = $"{longestDurationRun.Duration:0.##} min",
                DetailText = $"Dystans: {longestDurationRun.Distance:0.##} km",
                DateText = $"{longestDurationRun.Date:dd.MM.yyyy}",
                ExtraText = longestDurationRun.Distance > 0
                    ? $"Tempo: {FormatPace(longestDurationRun.Duration / longestDurationRun.Distance)} min/km"
                    : "-",
                AccentBackground = Color.FromArgb("#E0E7FF"),
                BadgeBackground = Color.FromArgb("#E0E7FF"),
                BadgeTextColor = Color.FromArgb("#4338CA")
            });
        }
    }

    private void BuildStrengthCategories()
    {
        StrengthCategories.Clear();

        var categories = _allWorkouts
            .Where(x => x.Type == "Siłownia" && !string.IsNullOrWhiteSpace(x.Category))
            .Select(x => x.Category!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        foreach (var category in categories)
        {
            StrengthCategories.Add(new RecordsOptionItem
            {
                Key = category,
                Title = category,
                Subtitle = "Wybierz partię do analizy",
                BackgroundColor = Color.FromArgb("#FFFFFF"),
                BorderColor = Color.FromArgb("#E2E8F0"),
                TextColor = Color.FromArgb("#1E293B")
            });
        }
    }

    private void BuildExercises(string strengthCategory)
    {
        Exercises.Clear();

        var exercises = _allWorkouts
            .Where(x => x.Type == "Siłownia"
                     && string.Equals(x.Category, strengthCategory, StringComparison.OrdinalIgnoreCase)
                     && !string.IsNullOrWhiteSpace(x.Exercise))
            .Select(x => x.Exercise!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        foreach (var exercise in exercises)
        {
            Exercises.Add(new RecordsOptionItem
            {
                Key = exercise,
                Title = exercise,
                Subtitle = "Pokaż rekordy dla ćwiczenia",
                BackgroundColor = Color.FromArgb("#FFFFFF"),
                BorderColor = Color.FromArgb("#E2E8F0"),
                TextColor = Color.FromArgb("#1E293B")
            });
        }
    }

    private void BuildStrengthExerciseRecords(string exercise)
    {
        Records.Clear();

        var workouts = _allWorkouts
            .Where(x => x.Type == "Siłownia"
                     && string.Equals(x.Exercise, exercise, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Date)
            .ToList();

        if (!workouts.Any())
            return;

        var allSets = workouts
            .Where(x => !string.IsNullOrWhiteSpace(x.GymSetsJson))
            .SelectMany(workout =>
            {
                var sets = DeserializeGymSets(workout.GymSetsJson);

                return sets.Select(set => new
                {
                    Workout = workout,
                    Set = set,
                    Volume = set.Weight * set.Reps
                });
            })
            .ToList();

        if (!allSets.Any())
            return;

        var bestWeight = allSets
            .OrderByDescending(x => x.Set.Weight)
            .ThenByDescending(x => x.Set.Reps)
            .FirstOrDefault();

        if (bestWeight != null)
        {
            Records.Add(new RecordItem
            {
                Icon = "🏋️",
                Title = "Największy ciężar",
                Subtitle = exercise,
                Category = "Siłownia",
                ValueText = $"{bestWeight.Set.Weight:0.##} kg",
                DetailText = $"Powtórzenia: {bestWeight.Set.Reps}",
                DateText = $"{bestWeight.Workout.Date:dd.MM.yyyy}",
                ExtraText = !string.IsNullOrWhiteSpace(bestWeight.Workout.Category)
                    ? $"Partia: {bestWeight.Workout.Category}"
                    : "-",
                AccentBackground = Color.FromArgb("#FFF7ED"),
                BadgeBackground = Color.FromArgb("#FFF7ED"),
                BadgeTextColor = Color.FromArgb("#C2410C")
            });
        }

        var bestVolumeSet = allSets
            .OrderByDescending(x => x.Volume)
            .ThenByDescending(x => x.Set.Weight)
            .FirstOrDefault();

        if (bestVolumeSet != null)
        {
            Records.Add(new RecordItem
            {
                Icon = "🔥",
                Title = "Najmocniejsza seria",
                Subtitle = exercise,
                Category = "Siłownia",
                ValueText = $"{bestVolumeSet.Volume:0.##} kg",
                DetailText = $"{bestVolumeSet.Set.Weight:0.##} kg × {bestVolumeSet.Set.Reps}",
                DateText = $"{bestVolumeSet.Workout.Date:dd.MM.yyyy}",
                ExtraText = !string.IsNullOrWhiteSpace(bestVolumeSet.Workout.Category)
                    ? $"Partia: {bestVolumeSet.Workout.Category}"
                    : "-",
                AccentBackground = Color.FromArgb("#FEF3C7"),
                BadgeBackground = Color.FromArgb("#FEF3C7"),
                BadgeTextColor = Color.FromArgb("#B45309")
            });
        }

        var bestWorkoutVolume = workouts
            .Select(workout =>
            {
                var sets = DeserializeGymSets(workout.GymSetsJson);
                var totalVolume = sets.Sum(x => x.Weight * x.Reps);

                return new
                {
                    Workout = workout,
                    TotalVolume = totalVolume
                };
            })
            .OrderByDescending(x => x.TotalVolume)
            .FirstOrDefault();

        if (bestWorkoutVolume != null && bestWorkoutVolume.TotalVolume > 0)
        {
            Records.Add(new RecordItem
            {
                Icon = "📈",
                Title = "Największa objętość treningu",
                Subtitle = exercise,
                Category = "Siłownia",
                ValueText = $"{bestWorkoutVolume.TotalVolume:0.##} kg",
                DetailText = "Suma ciężaru ze wszystkich serii",
                DateText = $"{bestWorkoutVolume.Workout.Date:dd.MM.yyyy}",
                ExtraText = !string.IsNullOrWhiteSpace(bestWorkoutVolume.Workout.Category)
                    ? $"Partia: {bestWorkoutVolume.Workout.Category}"
                    : "-",
                AccentBackground = Color.FromArgb("#FCE7F3"),
                BadgeBackground = Color.FromArgb("#FCE7F3"),
                BadgeTextColor = Color.FromArgb("#BE185D")
            });
        }
    }

    private void HighlightMainCategory(string key)
    {
        foreach (var item in Categories)
        {
            var selected = item.Key == key;

            item.BackgroundColor = selected ? Color.FromArgb("#0F172A") : Color.FromArgb("#FFFFFF");
            item.BorderColor = selected ? Color.FromArgb("#0F172A") : Color.FromArgb("#E2E8F0");
            item.TextColor = selected ? Colors.White : Color.FromArgb("#1E293B");
        }

        OnPropertyChanged(nameof(Categories));
    }

    private void HighlightStrengthCategory(string key)
    {
        foreach (var item in StrengthCategories)
        {
            var selected = item.Key == key;

            item.BackgroundColor = selected ? Color.FromArgb("#FFF7ED") : Color.FromArgb("#FFFFFF");
            item.BorderColor = selected ? Color.FromArgb("#FDBA74") : Color.FromArgb("#E2E8F0");
            item.TextColor = selected ? Color.FromArgb("#9A3412") : Color.FromArgb("#1E293B");
        }

        OnPropertyChanged(nameof(StrengthCategories));
    }

    private void HighlightExercise(string key)
    {
        foreach (var item in Exercises)
        {
            var selected = item.Key == key;

            item.BackgroundColor = selected ? Color.FromArgb("#EFF6FF") : Color.FromArgb("#FFFFFF");
            item.BorderColor = selected ? Color.FromArgb("#93C5FD") : Color.FromArgb("#E2E8F0");
            item.TextColor = selected ? Color.FromArgb("#1D4ED8") : Color.FromArgb("#1E293B");
        }

        OnPropertyChanged(nameof(Exercises));
    }

    private List<WorkoutSet> DeserializeGymSets(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<WorkoutSet>();

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<WorkoutSet>>(json) ?? new List<WorkoutSet>();
        }
        catch
        {
            return new List<WorkoutSet>();
        }
    }

    private string FormatPace(double minutesPerKm)
    {
        var minutes = (int)Math.Floor(minutesPerKm);
        var seconds = (int)Math.Round((minutesPerKm - minutes) * 60);

        if (seconds == 60)
        {
            minutes++;
            seconds = 0;
        }

        return $"{minutes}:{seconds:00}";
    }
}