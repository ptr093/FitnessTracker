using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessTracker.Models;
using FitnessTracker.Services;
using FitnessTracker.Views;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace FitnessTracker.ViewModels
{
    public partial class GymWorkoutViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly ExerciseCatalogService _catalogService;

        // Wspólny katalog ćwiczeń (domyślne + własne) z ExerciseCatalogService
        private Dictionary<string, List<string>> _exercisesDatabase = new();

        private string AddCustomItem => _catalogService.GetAddCustomExerciseItem();

        public ObservableCollection<string> Categories { get; } = new();
        public ObservableCollection<string> AvailableExercises { get; } = new();
        public ObservableCollection<WorkoutSet> Sets { get; } = new();

        // Ćwiczenia zapisane w bieżącej sesji (przycisk "Zapisz i dodaj kolejne")
        public ObservableCollection<string> SessionExercises { get; } = new();
        public bool HasSessionExercises => SessionExercises.Count > 0;

        public ObservableCollection<int> RestPresets { get; } = new() { 60, 90, 120, 180 };

        [ObservableProperty]
        private string? location;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelectedCategory))]
        private string? selectedCategory;

        public bool HasSelectedCategory => !string.IsNullOrEmpty(SelectedCategory);

        private string? _selectedExercise;
        public string? SelectedExercise
        {
            get => _selectedExercise;
            set
            {
                if (value != null && ExerciseCatalogService.IsAddCustomExerciseItem(value))
                {
                    PromptForCustomExerciseAsync();
                    return;
                }

                if (SetProperty(ref _selectedExercise, value))
                    _ = RefreshProgressionAsync();
            }
        }

        // ---------- Ostatni trening ----------
        [ObservableProperty]
        private string? lastWorkoutInfo;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotLoadingLastWorkout))]
        private bool isLoadingLastWorkout;

        public bool IsNotLoadingLastWorkout => !IsLoadingLastWorkout;

        // ---------- Progresja ----------
        private ProgressionSuggestion? _suggestion;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasProgressionInfo))]
        private string? progressionInfo;

        [ObservableProperty]
        private string? progressionDetails;

        public bool HasProgressionInfo => !string.IsNullOrEmpty(ProgressionInfo);

        [ObservableProperty]
        private bool canApplySuggestion;

        [ObservableProperty]
        private string currentOneRepMaxText = "—";

        [ObservableProperty]
        private string currentVolumeText = "0 kg";

        // ---------- Minutnik przerwy ----------
        private IDispatcherTimer? _restTimer;
        private DateTime _restEndsAt;

        [ObservableProperty]
        private int selectedRestSeconds = 90;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RestButtonText))]
        private bool isRestRunning;

        [ObservableProperty]
        private string restRemainingText = "1:30";

        [ObservableProperty]
        private double restProgress;

        public string RestButtonText => IsRestRunning ? "Stop" : "Start przerwy";

        public GymWorkoutViewModel(DatabaseService databaseService, ExerciseCatalogService catalogService)
        {
            _databaseService = databaseService;
            _catalogService = catalogService;

            LoadExercises();
            Location = Preferences.Default.Get("LastGymLocation", string.Empty);

            Sets.CollectionChanged += (_, e) =>
            {
                if (e.NewItems != null)
                    foreach (WorkoutSet s in e.NewItems) s.PropertyChanged += (_, _) => UpdateLiveStats();
                UpdateLiveStats();
            };
            SessionExercises.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasSessionExercises));

            Sets.Add(new WorkoutSet { SetNumber = 1, Weight = 0, Reps = 0 });
        }

        private void LoadExercises()
        {
            _exercisesDatabase = _catalogService.GetAllExercises();

            Categories.Clear();
            foreach (var category in _exercisesDatabase.Keys)
                Categories.Add(category);
        }

        private void FillExercises(string? category)
        {
            AvailableExercises.Clear();

            if (category == null || !_exercisesDatabase.TryGetValue(category, out var exercises))
                return;

            foreach (var ex in exercises)
                AvailableExercises.Add(ex);

            AvailableExercises.Add(AddCustomItem);
        }

        partial void OnSelectedCategoryChanging(string? value)
        {
            LastWorkoutInfo = null;
        }

        partial void OnSelectedCategoryChanged(string? value)
        {
            FillExercises(value);
            SelectedExercise = null;

            if (AvailableExercises.Count == 0)
                return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                SelectedExercise = AvailableExercises.FirstOrDefault();
            });
        }

        private async void PromptForCustomExerciseAsync()
        {
            var page = Shell.Current?.CurrentPage;
            if (page == null) return;

            string result = await page.DisplayPromptAsync(
                "Nowe ćwiczenie",
                "Podaj nazwę własnego ćwiczenia:",
                "Dodaj", "Anuluj",
                "np. Przyciąganie jednorącz");

            var currentCategory = SelectedCategory;
            string? newExercise = null;

            if (!string.IsNullOrWhiteSpace(result) && !string.IsNullOrEmpty(currentCategory))
            {
                _catalogService.AddCustomExercise(currentCategory, result);
                newExercise = $"⭐ {result.Trim().TrimStart('⭐').Trim()}";
                _exercisesDatabase = _catalogService.GetAllExercises();
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                FillExercises(currentCategory);
                SelectedExercise = newExercise != null && AvailableExercises.Contains(newExercise)
                    ? newExercise
                    : AvailableExercises.FirstOrDefault(x => !ExerciseCatalogService.IsAddCustomExerciseItem(x));
            });
        }

        // =====================================================
        // OSTATNI TRENING
        // =====================================================

        [RelayCommand]
        private async Task LoadLastWorkoutAsync()
        {
            if (IsLoadingLastWorkout)
                return;

            if (string.IsNullOrEmpty(SelectedCategory))
            {
                LastWorkoutInfo = "Najpierw wybierz partię mięśniową.";
                return;
            }

            IsLoadingLastWorkout = true;

            try
            {
                var last = await _databaseService.GetLastGymWorkoutAsync(SelectedCategory, SelectedExercise);

                if (last == null)
                {
                    LastWorkoutInfo = $"Brak zapisanych treningów dla partii „{SelectedCategory}”.";
                    return;
                }

                var sets = DatabaseService.GetOrderedSets(last);
                if (sets.Count == 0)
                {
                    LastWorkoutInfo = "Ostatni trening nie zawiera serii.";
                    return;
                }

                // Jeśli nie było historii dla wybranego ćwiczenia – przełączamy na ćwiczenie z ostatniego treningu partii
                if (last.Exercise != SelectedExercise)
                {
                    if (!AvailableExercises.Contains(last.Exercise))
                        AvailableExercises.Insert(Math.Max(0, AvailableExercises.Count - 1), last.Exercise);

                    SelectedExercise = last.Exercise;
                }

                ReplaceSets(sets);

                if (string.IsNullOrWhiteSpace(Location) && !string.IsNullOrWhiteSpace(last.Location))
                    Location = last.Location;

                var maxWeight = sets.Max(s => s.Weight);
                LastWorkoutInfo =
                    $"Wczytano: {last.Exercise} · {last.Date:dd.MM.yyyy} · {sets.Count} serii · max {maxWeight:0.##} kg";
            }
            catch (Exception ex)
            {
                LastWorkoutInfo = $"Nie udało się wczytać ostatniego treningu: {ex.Message}";
            }
            finally
            {
                IsLoadingLastWorkout = false;
            }
        }

        // =====================================================
        // PROGRESJA
        // =====================================================

        private List<WorkoutSet> _lastSetsForExercise = new();

        private async Task RefreshProgressionAsync()
        {
            var exercise = SelectedExercise;
            _suggestion = null;
            CanApplySuggestion = false;
            ProgressionInfo = null;
            ProgressionDetails = null;

            if (string.IsNullOrWhiteSpace(exercise))
                return;

            try
            {
                var last = await _databaseService.GetLastGymWorkoutAsync(null, exercise);

                // Użytkownik mógł w międzyczasie zmienić ćwiczenie
                if (exercise != SelectedExercise)
                    return;

                if (last == null)
                {
                    ProgressionInfo = "Pierwszy raz to ćwiczenie — zapisz wynik, a następnym razem podpowiem progresję.";
                    return;
                }

                _lastSetsForExercise = DatabaseService.GetOrderedSets(last);
                _suggestion = ProgressionCalculator.Suggest(_lastSetsForExercise);

                var setsText = string.Join(", ", _lastSetsForExercise.Select(s => $"{s.Weight:0.##}×{s.Reps}"));
                var e1rm = ProgressionCalculator.BestOneRepMax(_lastSetsForExercise);

                ProgressionDetails = $"Ostatnio ({last.Date:dd.MM}): {setsText} · 1RM ≈ {e1rm:0.#} kg";
                ProgressionInfo = _suggestion?.Message;
                CanApplySuggestion = _suggestion != null;
            }
            catch
            {
                // Brak podpowiedzi nie blokuje treningu
            }
        }

        [RelayCommand]
        private void ApplySuggestion()
        {
            if (_suggestion == null)
                return;

            var count = Math.Max(1, _lastSetsForExercise.Count(s => s.Weight > 0 && s.Reps > 0));
            var sets = Enumerable.Range(1, count)
                .Select(i => new WorkoutSet { SetNumber = i, Weight = _suggestion.Weight, Reps = _suggestion.Reps })
                .ToList();

            ReplaceSets(sets);
        }

        private void ReplaceSets(IEnumerable<WorkoutSet> sets)
        {
            Sets.Clear();
            foreach (var set in sets)
                Sets.Add(set);

            if (Sets.Count == 0)
                Sets.Add(new WorkoutSet { SetNumber = 1 });
        }

        private void UpdateLiveStats()
        {
            var valid = Sets.Where(s => s.Weight > 0 && s.Reps > 0).ToList();
            var e1rm = ProgressionCalculator.BestOneRepMax(valid);
            CurrentOneRepMaxText = e1rm > 0 ? $"{e1rm:0.#} kg" : "—";
            CurrentVolumeText = $"{ProgressionCalculator.Volume(valid):0.#} kg";
        }

        // =====================================================
        // SERIE
        // =====================================================

        [RelayCommand]
        private void AddSet()
        {
            // Nowa seria kopiuje ostatnią – zwykle robi się kilka serii tym samym ciężarem
            var last = Sets.LastOrDefault();
            Sets.Add(new WorkoutSet
            {
                SetNumber = Sets.Count + 1,
                Weight = last?.Weight ?? 0,
                Reps = last?.Reps ?? 0
            });

            if (Preferences.Default.Get("AutoStartRest", true) && last is { Weight: > 0, Reps: > 0 })
                StartRest();
        }

        [RelayCommand]
        private void RemoveSet(WorkoutSet set)
        {
            if (set == null || !Sets.Contains(set)) return;

            Sets.Remove(set);

            for (int i = 0; i < Sets.Count; i++)
                Sets[i].SetNumber = i + 1;

            if (Sets.Count == 0)
                Sets.Add(new WorkoutSet { SetNumber = 1, Weight = 0, Reps = 0 });
        }

        // =====================================================
        // MINUTNIK PRZERWY
        // =====================================================

        partial void OnSelectedRestSecondsChanged(int value)
        {
            if (!IsRestRunning)
                RestRemainingText = FormatSeconds(value);
        }

        [RelayCommand]
        private void SelectRest(int seconds)
        {
            SelectedRestSeconds = seconds;
            if (IsRestRunning)
                StartRest();
        }

        [RelayCommand]
        private void ToggleRest()
        {
            if (IsRestRunning)
                StopRest();
            else
                StartRest();
        }

        private void StartRest()
        {
            _restEndsAt = DateTime.Now.AddSeconds(SelectedRestSeconds);

            if (_restTimer == null)
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null) return;

                _restTimer = dispatcher.CreateTimer();
                _restTimer.Interval = TimeSpan.FromMilliseconds(250);
                _restTimer.Tick += (_, _) => OnRestTick();
            }

            IsRestRunning = true;
            OnRestTick();
            _restTimer.Start();
        }

        private void StopRest()
        {
            _restTimer?.Stop();
            IsRestRunning = false;
            RestProgress = 0;
            RestRemainingText = FormatSeconds(SelectedRestSeconds);
        }

        private void OnRestTick()
        {
            var remaining = _restEndsAt - DateTime.Now;

            if (remaining <= TimeSpan.Zero)
            {
                StopRest();
                RestRemainingText = "Czas na serię!";
                NotifyRestFinished();
                return;
            }

            RestRemainingText = FormatSeconds((int)Math.Ceiling(remaining.TotalSeconds));
            RestProgress = 1 - remaining.TotalSeconds / Math.Max(1, SelectedRestSeconds);
        }

        private static void NotifyRestFinished()
        {
            try
            {
                if (Vibration.Default.IsSupported)
                    Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(600));
                else
                    HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
            }
            catch
            {
                // Brak wibracji na danej platformie – ignorujemy
            }
        }

        private static string FormatSeconds(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

        public void OnPageDisappearing() => StopRest();

        // =====================================================
        // ZAPIS
        // =====================================================

        [RelayCommand]
        private Task SaveWorkoutAsync() => SaveCoreAsync(continueSession: false);

        [RelayCommand]
        private Task SaveAndContinueAsync() => SaveCoreAsync(continueSession: true);

        private async Task SaveCoreAsync(bool continueSession)
        {
            var page = Shell.Current?.CurrentPage;

            if (string.IsNullOrWhiteSpace(SelectedExercise) || ExerciseCatalogService.IsAddCustomExerciseItem(SelectedExercise))
            {
                if (page != null)
                    await page.DisplayAlert("Błąd", "Wybierz najpierw poprawne ćwiczenie!", "OK");
                return;
            }

            var validSets = Sets.Where(x => x.Weight > 0 && x.Reps > 0).ToList();
            if (!validSets.Any())
            {
                if (page != null)
                    await page.DisplayAlert("Błąd", "Dodaj co najmniej jedną poprawną serię!", "OK");
                return;
            }

            var workout = new Workout
            {
                Type = "Siłownia",
                Category = SelectedCategory ?? string.Empty,
                Exercise = SelectedExercise,
                GymSetsJson = JsonSerializer.Serialize(validSets),
                Date = DateTime.Now,
                Location = string.IsNullOrWhiteSpace(Location) ? null : Location.Trim()
            };

            var record = await _databaseService.GetExerciseRecordAsync(SelectedExercise);
            var currentMax = validSets.Max(x => x.Weight);
            var isNewRecord = !record.MaxWeight.HasValue || currentMax > record.MaxWeight.Value;

            await _databaseService.SaveWorkoutAsync(workout);
            Preferences.Default.Set("LastGymLocation", workout.Location ?? string.Empty);

            if (isNewRecord && page != null)
            {
                var popup = new NewPersonalRecordPopup(SelectedExercise, record.MaxWeight, currentMax, workout.Date);
                await page.ShowPopupAsync(popup);
            }

            if (continueSession)
            {
                SessionExercises.Add($"{workout.Exercise} · {validSets.Count} serii · max {currentMax:0.##} kg");
                StopRest();
                ReplaceSets(new[] { new WorkoutSet { SetNumber = 1 } });
                LastWorkoutInfo = null;
                await RefreshProgressionAsync();
                return;
            }

            StopRest();

            if (page != null)
            {
                var message = SessionExercises.Count > 0
                    ? $"Zapisano sesję: {SessionExercises.Count + 1} ćwiczeń."
                    : "Trening został zapisany!";
                await page.DisplayAlert("Sukces", message, "OK");
            }

            await Shell.Current!.GoToAsync("..");
        }
    }
}
