using CommunityToolkit.Maui.Views;
using FitnessTracker.Models;
using Microsoft.Maui.Controls.Shapes;
using System.Globalization;
using System.Text.Json;
using Microsoft.Maui.Storage;

namespace FitnessTracker.Views;

public partial class EditGymWorkoutPopup : Popup
{
    private readonly Workout _workout;
    private readonly Func<Workout, Task> _onSave;
    private readonly List<WorkoutSet> _sets;

    // Baza ćwiczeń (tak samo jak w GymWorkoutViewModel)
    private Dictionary<string, List<string>> _exercisesDatabase = new();
    private Dictionary<string, List<string>> _customUserExercises = new();

    public EditGymWorkoutPopup(Workout workout, Func<Workout, Task> onSave)
    {
        InitializeComponent();

        _workout = workout;
        _onSave = onSave;

        LoadExercises();
        InitializePickers();

        WorkoutDatePicker.Date = workout.Date == default ? DateTime.Today : workout.Date;

        _sets = string.IsNullOrWhiteSpace(workout.GymSetsJson)
            ? new List<WorkoutSet>()
            : JsonSerializer.Deserialize<List<WorkoutSet>>(workout.GymSetsJson) ?? new List<WorkoutSet>();

        if (_sets.Count == 0)
        {
            _sets.Add(new WorkoutSet
            {
                SetNumber = 1,
                Weight = 0,
                Reps = 0
            });
        }

        NormalizeSetNumbers();
        RenderSets();
    }

    // --- Ładowanie ćwiczeń (kopia logiki z GymWorkoutViewModel) ---

    private void LoadExercises()
    {
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

        string savedCustomJson = Preferences.Default.Get("CustomUserExercisesDB", string.Empty);

        if (!string.IsNullOrEmpty(savedCustomJson))
        {
            try
            {
                _customUserExercises = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(savedCustomJson) ?? new();

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
                _customUserExercises = new Dictionary<string, List<string>>();
            }
        }
    }

    private void SaveCustomExercise(string category, string newExercise)
    {
        if (_exercisesDatabase.ContainsKey(category) && !_exercisesDatabase[category].Contains(newExercise))
        {
            _exercisesDatabase[category].Add(newExercise);
        }

        if (!_customUserExercises.ContainsKey(category))
            _customUserExercises[category] = new List<string>();

        if (!_customUserExercises[category].Contains(newExercise))
        {
            _customUserExercises[category].Add(newExercise);
            string json = JsonSerializer.Serialize(_customUserExercises);
            Preferences.Default.Set("CustomUserExercisesDB", json);
        }
    }

    // --- Inicjalizacja Pickerów ---

    private void InitializePickers()
    {
        // Partie
        foreach (var category in _exercisesDatabase.Keys)
        {
            CategoryPicker.Items.Add(category);
        }

        // Ustawienie aktualnej partii
        var currentCategory = string.IsNullOrWhiteSpace(_workout.Category)
            ? null
            : _exercisesDatabase.Keys.FirstOrDefault(k => k == _workout.Category);

        if (currentCategory != null)
            CategoryPicker.SelectedItem = currentCategory;

        // Obsługa zmiany partii
        CategoryPicker.SelectedIndexChanged += (s, e) =>
        {
            var selectedCategory = CategoryPicker.SelectedItem as string;
            ExercisePicker.Items.Clear();

            if (selectedCategory == null || !_exercisesDatabase.ContainsKey(selectedCategory))
                return;

            foreach (var ex in _exercisesDatabase[selectedCategory])
            {
                ExercisePicker.Items.Add(ex);
            }

            ExercisePicker.Items.Add("➕ Dodaj własne ćwiczenie...");

            // Ustawienie aktualnego ćwiczenia
            var currentExercise = string.IsNullOrWhiteSpace(_workout.Exercise)
                ? null
                : _exercisesDatabase[selectedCategory].FirstOrDefault(x => x == _workout.Exercise);

            ExercisePicker.SelectedItem = currentExercise ?? ExercisePicker.Items.FirstOrDefault();
        };

        // Obsługa dodawania własnego ćwiczenia
        ExercisePicker.SelectedIndexChanged += (s, e) =>
        {
            var selectedExercise = ExercisePicker.SelectedItem as string;

            if (selectedExercise == "➕ Dodaj własne ćwiczenie...")
            {
                PromptForCustomExerciseAsync();
            }
        };
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
            string currentCategory = CategoryPicker.SelectedItem as string;

            if (!string.IsNullOrEmpty(currentCategory))
            {
                SaveCustomExercise(currentCategory, newExercise);
            }

            // Odśwież listę ćwiczeń
            ExercisePicker.Items.Clear();

            if (!string.IsNullOrEmpty(currentCategory) && _exercisesDatabase.ContainsKey(currentCategory))
            {
                foreach (var item in _exercisesDatabase[currentCategory])
                {
                    ExercisePicker.Items.Add(item);
                }
            }
            ExercisePicker.Items.Add("➕ Dodaj własne ćwiczenie...");

            ExercisePicker.SelectedItem = newExercise;
        }
        else
        {
            // Wróć do poprzedniego wyboru
            ExercisePicker.SelectedItem = ExercisePicker.Items.FirstOrDefault(x => x != "➕ Dodaj własne ćwiczenie...");
        }
    }

    // --- Renderowanie serii (bez zmian w logice) ---

    private void RenderSets()
    {
        SetsContainer.Children.Clear();

        foreach (var set in _sets.ToList())
        {
            var currentSet = set;

            var titleLabel = new Label
            {
                Text = $"Seria {currentSet.SetNumber}",
                VerticalOptions = LayoutOptions.Center,
                FontAttributes = FontAttributes.Bold,
                FontSize = 14,
                TextColor = Color.FromArgb("#0F172A")
            };

            var deleteButton = new Button
            {
                Text = "Usuń",
                BackgroundColor = Color.FromArgb("#FEF2F2"),
                TextColor = Color.FromArgb("#DC2626"),
                CornerRadius = 10,
                HeightRequest = 36,
                FontAttributes = FontAttributes.Bold,
                Padding = new Thickness(12, 0)
            };

            deleteButton.Clicked += (_, __) =>
            {
                _sets.Remove(currentSet);

                if (_sets.Count == 0)
                {
                    _sets.Add(new WorkoutSet
                    {
                        SetNumber = 1,
                        Weight = 0,
                        Reps = 0
                    });
                }

                NormalizeSetNumbers();
                RenderSets();
            };

            var headerGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                ColumnSpacing = 8
            };

            headerGrid.Add(titleLabel, 0, 0);
            headerGrid.Add(deleteButton, 1, 0);

            var weightEntry = new Entry
            {
                Text = currentSet.Weight.ToString("0.##", CultureInfo.InvariantCulture),
                Keyboard = Keyboard.Numeric,
                HorizontalTextAlignment = TextAlignment.Center,
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#0F172A"),
                HeightRequest = 44
            };

            var repsEntry = new Entry
            {
                Text = currentSet.Reps.ToString(),
                Keyboard = Keyboard.Numeric,
                HorizontalTextAlignment = TextAlignment.Center,
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#0F172A"),
                HeightRequest = 44
            };

            weightEntry.TextChanged += (_, e) =>
            {
                var value = (e.NewTextValue ?? string.Empty).Replace(',', '.');

                if (string.IsNullOrWhiteSpace(value))
                {
                    currentSet.Weight = 0;
                    return;
                }

                if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                    currentSet.Weight = parsed;
            };

            repsEntry.TextChanged += (_, e) =>
            {
                var value = e.NewTextValue ?? string.Empty;

                if (string.IsNullOrWhiteSpace(value))
                {
                    currentSet.Reps = 0;
                    return;
                }

                if (int.TryParse(value, out var parsed))
                    currentSet.Reps = parsed;
            };

            var inputsGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star }
                },
                ColumnSpacing = 10
            };

            var weightBlock = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label
                    {
                        Text = "Ciężar (kg)",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#64748B")
                    },
                    new Border
                    {
                        StrokeShape = new RoundRectangle { CornerRadius = 12 },
                        BackgroundColor = Colors.White,
                        Stroke = Color.FromArgb("#E2E8F0"),
                        StrokeThickness = 1,
                        Padding = new Thickness(10,0),
                        Content = weightEntry
                    }
                }
            };

            var repsBlock = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label
                    {
                        Text = "Powtórzenia",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#64748B")
                    },
                    new Border
                    {
                        StrokeShape = new RoundRectangle { CornerRadius = 12 },
                        BackgroundColor = Colors.White,
                        Stroke = Color.FromArgb("#E2E8F0"),
                        StrokeThickness = 1,
                        Padding = new Thickness(10,0),
                        Content = repsEntry
                    }
                }
            };

            inputsGrid.Add(weightBlock, 0, 0);
            inputsGrid.Add(repsBlock, 1, 0);

            var card = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                BackgroundColor = Color.FromArgb("#F8FAFC"),
                Stroke = Color.FromArgb("#E2E8F0"),
                StrokeThickness = 1,
                Padding = 14,
                Content = new VerticalStackLayout
                {
                    Spacing = 10,
                    Children =
                    {
                        headerGrid,
                        inputsGrid
                    }
                }
            };

            SetsContainer.Children.Add(card);
        }
    }

    private void NormalizeSetNumbers()
    {
        for (int i = 0; i < _sets.Count; i++)
        {
            _sets[i].SetNumber = i + 1;
        }
    }

    private void OnAddSetClicked(object sender, EventArgs e)
    {
        _sets.Add(new WorkoutSet
        {
            SetNumber = _sets.Count + 1,
            Weight = 0,
            Reps = 0
        });

        NormalizeSetNumbers();
        RenderSets();
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        Close();
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var category = CategoryPicker.SelectedItem as string ?? string.Empty;
        var exercise = ExercisePicker.SelectedItem as string ?? string.Empty;

        if (string.IsNullOrWhiteSpace(category))
        {
            await ShowValidationAlert("Wybierz partię mięśniową.");
            return;
        }

        if (string.IsNullOrWhiteSpace(exercise) || exercise == "➕ Dodaj własne ćwiczenie...")
        {
            await ShowValidationAlert("Wybierz ćwiczenie.");
            return;
        }

        NormalizeSetNumbers();

        var validSets = _sets
            .Where(x => x.Weight > 0 || x.Reps > 0)
            .Select((x, index) => new WorkoutSet
            {
                SetNumber = index + 1,
                Weight = x.Weight,
                Reps = x.Reps
            })
            .ToList();

        if (validSets.Count == 0)
        {
            await ShowValidationAlert("Dodaj przynajmniej jedną serię z ciężarem lub powtórzeniami.");
            return;
        }

        _workout.Category = category;
        _workout.Exercise = exercise;
        _workout.Date = WorkoutDatePicker.Date;
        _workout.GymSetsJson = JsonSerializer.Serialize(validSets);

        if (_onSave != null)
            await _onSave(_workout);

        Close();
    }

    private static Task ShowValidationAlert(string message)
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Application.Current?.MainPage is Page page)
            {
                await page.DisplayAlert("Uwaga", message, "OK");
            }
        });
    }
}