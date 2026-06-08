using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessTracker.Models;
using FitnessTracker.Services;
using FitnessTracker.Views;
using Microsoft.Maui.Graphics;

namespace FitnessTracker.ViewModels
{
    public partial class HistoryViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private List<Workout> _allWorkouts = new();

        public ObservableCollection<HistoryDayItem> TrainingDays { get; } = new();
        public ObservableCollection<HistoryWorkoutItem> FilteredWorkouts { get; } = new();

        [ObservableProperty]
        private string selectedDayTitle = "Historia treningów";

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        private bool showEmptyState;

        public HistoryViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            try
            {
                IsRefreshing = true;

                var workouts = await _databaseService.GetWorkoutsAsync();
                _allWorkouts = workouts
                    .OrderByDescending(x => x.Date)
                    .ToList();

                BuildTrainingDays();

                var latestDay = TrainingDays.FirstOrDefault();
                if (latestDay != null)
                {
                    ApplySelectedDay(latestDay.Date);
                }
                else
                {
                    SelectedDayTitle = "Brak zapisanych treningów";
                    FilteredWorkouts.Clear();
                    ShowEmptyState = true;
                }
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        [RelayCommand]
        private void SelectDay(HistoryDayItem dayItem)
        {
            if (dayItem == null)
                return;

            ApplySelectedDay(dayItem.Date);
        }

        [RelayCommand]
        private async Task DeleteWorkoutAsync(HistoryWorkoutItem item)
        {
            if (item == null)
                return;

            bool confirm = false;

            if (App.Current?.MainPage != null)
            {
                confirm = await App.Current.MainPage.DisplayAlert(
                    "Usuń trening",
                    "Czy na pewno chcesz usunąć ten trening z historii?",
                    "Usuń",
                    "Anuluj");
            }

            if (!confirm)
                return;

            var workoutToDelete = _allWorkouts.FirstOrDefault(x => x.Id == item.WorkoutId);
            if (workoutToDelete == null)
                return;

            await _databaseService.DeleteWorkoutAsync(workoutToDelete);
            await LoadDataAsync();
        }

        [RelayCommand]
        private async Task EditWorkoutAsync(HistoryWorkoutItem item)
        {
            if (item == null || App.Current?.MainPage == null)
                return;

            var workout = _allWorkouts.FirstOrDefault(x => x.Id == item.WorkoutId);
            if (workout == null)
                return;

            if (App.Current.MainPage is not Page page)
                return;

            if (workout.Type == "Siłownia")
            {
                var popup = new EditGymWorkoutPopup(workout, async updatedWorkout =>
                {
                    await _databaseService.UpdateWorkoutAsync(updatedWorkout);
                    await LoadDataAsync();
                });

                await page.ShowPopupAsync(popup);
            }
            else if (workout.Type == "Bieganie")
            {
                var popup = new EditRunWorkoutPopup(workout, async updatedWorkout =>
                {
                    await _databaseService.UpdateWorkoutAsync(updatedWorkout);
                    await LoadDataAsync();
                });

                await page.ShowPopupAsync(popup);
            }
        }

        private void BuildTrainingDays()
        {
            TrainingDays.Clear();

            var groupedDays = _allWorkouts
                .GroupBy(x => x.Date.Date)
                .OrderByDescending(g => g.Key)
                .ToList();

            foreach (var day in groupedDays)
            {
                TrainingDays.Add(new HistoryDayItem
                {
                    Date = day.Key,
                    DayLabel = $"{day.Key:dd}\n{day.Key:MM}",
                    BackgroundColor = Color.FromArgb("#F8FAFC"),
                    TextColor = Color.FromArgb("#0F172A")
                });
            }
        }

        private void ApplySelectedDay(DateTime selectedDate)
        {
            HighlightSelectedDay(selectedDate);

            SelectedDayTitle = $"Treningi z dnia {selectedDate:dd.MM.yyyy}";
            FilteredWorkouts.Clear();

            var workoutsForDay = _allWorkouts
                .Where(x => x.Date.Date == selectedDate.Date)
                .OrderByDescending(x => x.Date)
                .ToList();

            foreach (var workout in workoutsForDay)
            {
                FilteredWorkouts.Add(MapWorkoutToHistoryItem(workout));
            }

            ShowEmptyState = !FilteredWorkouts.Any();
        }

        private void HighlightSelectedDay(DateTime selectedDate)
        {
            foreach (var day in TrainingDays)
            {
                var isSelected = day.Date.Date == selectedDate.Date;

                day.BackgroundColor = isSelected
                    ? Color.FromArgb("#0F172A")
                    : Color.FromArgb("#F8FAFC");

                day.TextColor = isSelected
                    ? Colors.White
                    : Color.FromArgb("#0F172A");
            }
        }

        private HistoryWorkoutItem MapWorkoutToHistoryItem(Workout workout)
        {
            var item = new HistoryWorkoutItem
            {
                WorkoutId = workout.Id,
                HeaderTitle = workout.Type == "Bieganie"
                    ? "Bieg"
                    : (!string.IsNullOrWhiteSpace(workout.Exercise) ? workout.Exercise : "Trening siłowy"),
                HeaderSubtitle = $"{workout.Date:HH:mm}",
                BadgeText = workout.Type,
                IsExpanded = false
            };

            if (workout.Type == "Siłownia")
            {
                item.BadgeBackground = Color.FromArgb("#DBEAFE");
                item.BadgeTextColor = Color.FromArgb("#1D4ED8");

                var lines = new ObservableCollection<string>();

                if (!string.IsNullOrWhiteSpace(workout.Category))
                    lines.Add($"Partia: {workout.Category}");

                if (!string.IsNullOrWhiteSpace(workout.Exercise))
                    lines.Add($"Ćwiczenie: {workout.Exercise}");

                var sets = DeserializeGymSets(workout.GymSetsJson);

                if (sets.Any())
                {
                    foreach (var set in sets)
                        lines.Add($"Seria {set.SetNumber}: {set.Weight} kg x {set.Reps} powt.");

                    var totalVolume = sets.Sum(x => x.Weight * x.Reps);
                    lines.Add($"Objętość: {totalVolume:0.##} kg");
                }
                else
                {
                    lines.Add("Brak zapisanych serii.");
                }

                item.DetailLines = lines;
            }
            else if (workout.Type == "Bieganie")
            {
                item.BadgeBackground = Color.FromArgb("#DCFCE7");
                item.BadgeTextColor = Color.FromArgb("#15803D");

                var lines = new ObservableCollection<string>
                {
                    $"Dystans: {workout.Distance:0.##} km",
                    $"Czas: {workout.Duration:0.##} min"
                };

                if (workout.Distance > 0 && workout.Duration > 0)
                {
                    var avgPace = workout.Duration / workout.Distance;
                    lines.Add($"Średnie tempo: {FormatPace(avgPace)} min/km");
                }

                var splits = DeserializeRunSplits(workout.SplitsJson);

                if (splits.Any())
                {
                    for (int i = 0; i < splits.Count; i++)
                    {
                        var speed = splits[i];
                        lines.Add($"Split {i + 1}: {speed:0.##} km/h ({ConvertSpeedToPace(speed)}/km)");
                    }
                }
                else
                {
                    lines.Add("Brak zapisanych międzyczasów.");
                }

                item.DetailLines = lines;
            }

            return item;
        }

        private List<WorkoutSet> DeserializeGymSets(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new();

            try
            {
                return JsonSerializer.Deserialize<List<WorkoutSet>>(json) ?? new();
            }
            catch
            {
                return new();
            }
        }

        private List<double> DeserializeRunSplits(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new();

            try
            {
                return JsonSerializer.Deserialize<List<double>>(json) ?? new();
            }
            catch
            {
                return new();
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

        private string ConvertSpeedToPace(double speedKmH)
        {
            if (speedKmH <= 0)
                return "-";

            var totalMinutesPerKm = 60.0 / speedKmH;
            return FormatPace(totalMinutesPerKm);
        }
    }
}