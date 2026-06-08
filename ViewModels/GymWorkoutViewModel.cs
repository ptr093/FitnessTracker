using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessTracker.Models;
using FitnessTracker.Services;
using FitnessTracker.Views;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage; // Dodane do obsługi zapisu w pamięci urządzenia
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace FitnessTracker.ViewModels
{
    public partial class GymWorkoutViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        // Główna lista ćwiczeń (połączona: Domyślne + Własne użytkownika)
        private Dictionary<string, List<string>> _exercisesDatabase = new();

        // Lista tylko własnych ćwiczeń użytkownika (do zapisywania w pamięci)
        private Dictionary<string, List<string>> _customUserExercises = new();

        public ObservableCollection<string> Categories { get; } = new()
        {
            "Klatka piersiowa", "Plecy", "Nogi", "Barki", "Ramiona", "Brzuch"
        };

        public ObservableCollection<string> AvailableExercises { get; } = new();
        public ObservableCollection<WorkoutSet> Sets { get; } = new();

        [ObservableProperty]
        private string selectedCategory;

        private string _selectedExercise;
        public string SelectedExercise
        {
            get => _selectedExercise;
            set
            {
                if (value == "➕ Dodaj własne ćwiczenie...")
                {
                    PromptForCustomExerciseAsync();
                }
                else
                {
                    SetProperty(ref _selectedExercise, value);
                }
            }
        }

        public GymWorkoutViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;

            // Ładujemy ćwiczenia przy wejściu do ViewModelu
            LoadExercises();

            Sets.Add(new WorkoutSet { SetNumber = 1, Weight = 0, Reps = 0 });
        }

        // Metoda ładująca i łącząca bazę ćwiczeń
        private void LoadExercises()
        {
            // 1. Definiujemy bazę domyślną (sztywną)
            _exercisesDatabase = new Dictionary<string, List<string>>
            {
                { "Klatka piersiowa", new List<string> { "🏋️ Wyciskanie sztangi leżąc", "🏋️ Wyciskanie sztangi skos górny", "🔩 Wyciskanie hantli leżąc", "🔩 Wyciskanie hantli skos", "🤖 Hammer (Wyciskanie maszyna)", "🦋 Rozpiętki hantlami", "🦾 Pompki na poręczach (Dips)" } },
                { "Plecy", new List<string> { "🦅 Ściąganie drążka (Lat Pulldown)", "🦅 Ściąganie chwytu wyciągu górnego po skosie", "🚣 Wiosłowanie sztangą", "🚣 Przyciąganie linki wyciągu dolnego siedząc", "🧗 Podciąganie na drążku", "🔥 Martwy ciąg" } },
                { "Nogi", new List<string> { "🦵 Przysiad ze sztangą (Squat)", "🏗️ Wyciskanie na suwnicy", "🍑 Hip Thrust (Unoszenie bioder)", "🚶 Wykroki", "🦵 Uginanie nóg na maszynie", "🦵 Wznosy na łydki" } },
                { "Barki", new List<string>
                {
                 "🛡️ Wyciskanie żołnierskie (OHP)",
                 "🔩 Wyciskanie hantli siedząc",
                 "🦅 Wznosy bokiem",
                 "🦅 Wznosy w opadzie",
                 "🦋 Odwodzenie ramion na maszynie Butterfly odwrotnie (Rear Delt Fly)",
                 "🎯 Face pulls",
                 "⬆️ Unoszenie hantli w przód"
                    }
                },
                { "Ramiona", new List<string> { "💪 Uginanie ze sztangą (Biceps)", "🔨 Uginanie młotkowe", "💪 Wyciskanie francuskie", "🪢 Prostowanie z linką wyciągu" } },
                { "Brzuch", new List<string> { "🍫 Allahy (Crunches z linką)", "🚲 Rowerek", "🛹 Deska (Plank)", "🧗 Unoszenie nóg w zwisie" } }
            };

            // 2. Odczytujemy zapisane własne ćwiczenia z urządzenia (Preferences)
            string savedCustomJson = Preferences.Default.Get("CustomUserExercisesDB", string.Empty);

            if (!string.IsNullOrEmpty(savedCustomJson))
            {
                try
                {
                    _customUserExercises = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(savedCustomJson) ?? new();

                    // 3. Łączymy: dodajemy własne ćwiczenia do głównego słownika
                    foreach (var category in _customUserExercises)
                    {
                        if (_exercisesDatabase.ContainsKey(category.Key))
                        {
                            foreach (var customEx in category.Value)
                            {
                                if (!_exercisesDatabase[category.Key].Contains(customEx))
                                    _exercisesDatabase[category.Key].Add(customEx);
                            }
                        }
                    }
                }
                catch
                {
                    // W razie błędu parsowania, ignorujemy
                    _customUserExercises = new Dictionary<string, List<string>>();
                }
            }
        }

        // Zapisuje nowe ćwiczenie trwale na urządzeniu
        private void SaveCustomExercise(string category, string newExercise)
        {
            // Dodajemy do słownika głównego (żeby działało od razu)
            if (_exercisesDatabase.ContainsKey(category) && !_exercisesDatabase[category].Contains(newExercise))
            {
                _exercisesDatabase[category].Add(newExercise);
            }

            // Dodajemy do słownika z samymi "własnymi" i zapisujemy do pamięci
            if (!_customUserExercises.ContainsKey(category))
                _customUserExercises[category] = new List<string>();

            if (!_customUserExercises[category].Contains(newExercise))
            {
                _customUserExercises[category].Add(newExercise);

                // Zapis na stałe!
                string json = JsonSerializer.Serialize(_customUserExercises);
                Preferences.Default.Set("CustomUserExercisesDB", json);
            }
        }

        partial void OnSelectedCategoryChanged(string value)
        {
            AvailableExercises.Clear();
            SelectedExercise = null;

            if (value == null || !_exercisesDatabase.ContainsKey(value))
                return;

            var exercises = _exercisesDatabase[value];

            foreach (var ex in exercises)
            {
                AvailableExercises.Add(ex);
            }

            AvailableExercises.Add("➕ Dodaj własne ćwiczenie...");

            MainThread.BeginInvokeOnMainThread(() =>
            {
                SelectedExercise = AvailableExercises.FirstOrDefault();
            });
        }

        private async void PromptForCustomExerciseAsync()
        {
            if (App.Current?.MainPage == null) return;

            string result = await App.Current.MainPage.DisplayPromptAsync(
                "Nowe ćwiczenie",
                "Podaj nazwę własnego ćwiczenia:",
                "Dodaj", "Anuluj",
                "np. Przyciąganie jednorącz");

            if (!string.IsNullOrWhiteSpace(result))
            {
                string newExercise = $"⭐ {result.Trim()}";
                string currentCategory = SelectedCategory;

                if (!string.IsNullOrEmpty(currentCategory))
                {
                    // TRWAŁY ZAPIS
                    SaveCustomExercise(currentCategory, newExercise);
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AvailableExercises.Clear();

                    if (!string.IsNullOrEmpty(currentCategory) && _exercisesDatabase.ContainsKey(currentCategory))
                    {
                        foreach (var item in _exercisesDatabase[currentCategory])
                        {
                            AvailableExercises.Add(item);
                        }
                    }
                    AvailableExercises.Add("➕ Dodaj własne ćwiczenie...");

                    SelectedExercise = newExercise;
                });
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SelectedExercise = AvailableExercises.FirstOrDefault();
                });
            }
        }

        [RelayCommand]
        private void AddSet()
        {
            Sets.Add(new WorkoutSet { SetNumber = Sets.Count + 1, Weight = 0, Reps = 0 });
        }

        [RelayCommand]
        private void RemoveSet(WorkoutSet set)
        {
            if (set == null || !Sets.Contains(set)) return;

            Sets.Remove(set);

            for (int i = 0; i < Sets.Count; i++)
            {
                Sets[i].SetNumber = i + 1;
            }

            if (Sets.Count == 0)
            {
                Sets.Add(new WorkoutSet { SetNumber = 1, Weight = 0, Reps = 0 });
            }
        }

        [RelayCommand]
       
        private async Task SaveWorkoutAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedExercise) || SelectedExercise == "➕ Dodaj własne ćwiczenie...")
            {
                if (App.Current?.MainPage != null)
                    await App.Current.MainPage.DisplayAlert("Błąd", "Wybierz najpierw poprawne ćwiczenie!", "OK");
                return;
            }

            var validSets = Sets.Where(x => x.Weight > 0 && x.Reps > 0).ToList();
            if (!validSets.Any())
            {
                if (App.Current?.MainPage != null)
                    await App.Current.MainPage.DisplayAlert("Błąd", "Dodaj co najmniej jedną poprawną serię!", "OK");
                return;
            }

            var workout = new Workout
            {
                Type = "Siłownia",
                Category = SelectedCategory,
                Exercise = SelectedExercise,
                GymSetsJson = JsonSerializer.Serialize(validSets),
                Date = DateTime.Now
            };

            var record = await _databaseService.GetExerciseRecordAsync(SelectedExercise);
            var currentMax = validSets.Max(x => x.Weight);
            var isNewRecord = !record.MaxWeight.HasValue || currentMax > record.MaxWeight.Value;

            await _databaseService.SaveWorkoutAsync(workout);

            if (isNewRecord && App.Current?.MainPage is not null)
            {
                var popup = new NewPersonalRecordPopup(
                    SelectedExercise,
                    record.MaxWeight,
                    currentMax,
                    workout.Date);

                if (App.Current.MainPage is Page page)
                    await page.ShowPopupAsync(popup);
            }

            if (App.Current?.MainPage != null)
                await App.Current.MainPage.DisplayAlert("Sukces", "Trening został zapisany!", "OK");

            await Shell.Current.GoToAsync("..");
        }
    }
}
