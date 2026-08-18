using CommunityToolkit.Maui.Views;
using FitnessTracker.Models;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Storage;
using System.Globalization;
using System.Text.Json;

namespace FitnessTracker.Views;

public partial class EditGymWorkoutPopup : Popup
{
    private const string AddCustomExerciseItem = "➕ Dodaj własne ćwiczenie...";
    private const string CustomExercisePrefix = "⭐ ";
    private const string CustomExercisesKey = "CustomUserExercisesDB";

    private readonly Workout _workout;
    private readonly Func<Workout, Task> _onSave;
    private readonly List<WorkoutSet> _sets;

    private Dictionary<string, List<string>> _exercisesDatabase = new();
    private Dictionary<string, List<string>> _customUserExercises = new();

    private bool _isInitializingPickers;

    public EditGymWorkoutPopup(Workout workout, Func<Workout, Task> onSave)
    {
        InitializeComponent();

        _workout = workout ?? throw new ArgumentNullException(nameof(workout));
        _onSave = onSave ?? throw new ArgumentNullException(nameof(onSave));

        _exercisesDatabase = CreateDefaultExercisesDatabase();
        _customUserExercises = LoadCustomExercises();
        MergeCustomExercises();

        WorkoutDatePicker.Date = workout.Date == default
            ? DateTime.Today
            : workout.Date;

        LocationEntry.Text = workout.Location;

        _sets = DeserializeGymSets(workout.GymSetsJson);

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
        InitializePickers(workout.Category, workout.Exercise);
        RenderSets();
    }

    private void InitializePickers(string? savedCategory, string? savedExercise)
    {
        _isInitializingPickers = true;

        CategoryPicker.SelectedIndexChanged -= OnCategoryPickerChanged;
        ExercisePicker.SelectedIndexChanged -= OnExercisePickerChanged;

        CategoryPicker.Items.Clear();
        ExercisePicker.Items.Clear();

        foreach (var category in _exercisesDatabase.Keys)
        {
            CategoryPicker.Items.Add(category);
        }

        var categoryIndex = FindItemIndex(CategoryPicker.Items, savedCategory);

        // Dla starych treningów z pustą lub zmienioną kategorią:
        // wybieramy pierwszą kategorię, aby użytkownik mógł kontynuować edycję.
        if (categoryIndex < 0 && CategoryPicker.Items.Count > 0)
        {
            categoryIndex = 0;
        }

        CategoryPicker.SelectedIndex = categoryIndex;

        FillExercisesForSelectedCategory(savedExercise);

        CategoryPicker.SelectedIndexChanged += OnCategoryPickerChanged;
        ExercisePicker.SelectedIndexChanged += OnExercisePickerChanged;

        _isInitializingPickers = false;
    }

    private void OnCategoryPickerChanged(object? sender, EventArgs e)
    {
        if (_isInitializingPickers)
            return;

        FillExercisesForSelectedCategory();
    }

    private void FillExercisesForSelectedCategory(string? exerciseToSelect = null)
    {
        ExercisePicker.Items.Clear();
        ExercisePicker.SelectedIndex = -1;

        if (CategoryPicker.SelectedIndex < 0)
            return;

        var category = CategoryPicker.Items[CategoryPicker.SelectedIndex];

        if (!_exercisesDatabase.TryGetValue(category, out var exercises))
            return;

        foreach (var exercise in exercises)
        {
            ExercisePicker.Items.Add(exercise);
        }

        var exerciseIndex = FindItemIndex(
            ExercisePicker.Items,
            exerciseToSelect);

        // Jeśli trening historyczny zawiera ćwiczenie, którego nie ma już
        // na liście, dodaj je czasowo, aby nie stracić wartości przy zapisie.
        if (exerciseIndex < 0 && !string.IsNullOrWhiteSpace(exerciseToSelect))
        {
            ExercisePicker.Items.Add(exerciseToSelect);
            exerciseIndex = ExercisePicker.Items.Count - 1;
        }

        ExercisePicker.Items.Add(AddCustomExerciseItem);

        if (exerciseIndex >= 0)
        {
            ExercisePicker.SelectedIndex = exerciseIndex;
        }
        else if (ExercisePicker.Items.Count > 1)
        {
            // Pierwszy element to realne ćwiczenie; ostatni to „Dodaj własne”.
            ExercisePicker.SelectedIndex = 0;
        }
    }

    private void OnExercisePickerChanged(object? sender, EventArgs e)
    {
        if (_isInitializingPickers)
            return;

        if (ExercisePicker.SelectedIndex < 0)
            return;

        var selectedExercise = ExercisePicker.Items[ExercisePicker.SelectedIndex];

        if (selectedExercise == AddCustomExerciseItem)
        {
            PromptForCustomExerciseAsync();
        }
    }

    private async void PromptForCustomExerciseAsync()
    {
        if (App.Current?.MainPage is null)
            return;

        var enteredName = await App.Current.MainPage.DisplayPromptAsync(
            "Nowe ćwiczenie",
            "Podaj nazwę własnego ćwiczenia:",
            "Dodaj",
            "Anuluj",
            "np. Przyciąganie jednorącz");

        if (string.IsNullOrWhiteSpace(enteredName))
        {
            FillExercisesForSelectedCategory(_workout.Exercise);
            return;
        }

        if (CategoryPicker.SelectedIndex < 0)
            return;

        var category = CategoryPicker.Items[CategoryPicker.SelectedIndex];

        var cleanName = enteredName
            .Trim()
            .TrimStart('⭐')
            .Trim();

        if (string.IsNullOrWhiteSpace(cleanName))
        {
            FillExercisesForSelectedCategory(_workout.Exercise);
            return;
        }

        var newExercise = $"{CustomExercisePrefix}{cleanName}";

        SaveCustomExercise(category, newExercise);
        FillExercisesForSelectedCategory(newExercise);
    }

    private void SaveCustomExercise(string category, string newExercise)
    {
        if (!_exercisesDatabase.TryGetValue(category, out var allExercises))
            return;

        if (!allExercises.Contains(newExercise))
        {
            allExercises.Add(newExercise);
        }

        if (!_customUserExercises.TryGetValue(category, out var customExercises))
        {
            customExercises = new List<string>();
            _customUserExercises[category] = customExercises;
        }

        if (!customExercises.Any(x =>
                string.Equals(x, newExercise, StringComparison.OrdinalIgnoreCase)))
        {
            customExercises.Add(newExercise);

            Preferences.Default.Set(
                CustomExercisesKey,
                JsonSerializer.Serialize(_customUserExercises));
        }
    }

    private static int FindItemIndex(IList<string> items, string? searchedValue)
    {
        if (string.IsNullOrWhiteSpace(searchedValue))
            return -1;

        for (var i = 0; i < items.Count; i++)
        {
            if (string.Equals(
                    items[i].Trim(),
                    searchedValue.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static Dictionary<string, List<string>> CreateDefaultExercisesDatabase()
    {
        return new Dictionary<string, List<string>>
        {
            ["Klatka piersiowa"] = new List<string>
            {
                "🏋️ Wyciskanie sztangi leżąc",
                "🏋️ Wyciskanie sztangi skos górny",
                "🔩 Wyciskanie hantli leżąc",
                "🔩 Wyciskanie hantli skos",
                "🤖 Hammer (Wyciskanie maszyna)",
                "🦋 Rozpiętki hantlami",
                "🦾 Pompki na poręczach (Dips)"
            },

            ["Plecy"] = new List<string>
            {
                "🦅 Ściąganie drążka (Lat Pulldown)",
                "🦅 Ściąganie chwytu wyciągu górnego po skosie",
                "🚣 Wiosłowanie sztangą",
                "🚣 Przyciąganie linki wyciągu dolnego siedząc",
                "🧗 Podciąganie na drążku",
                "🔥 Martwy ciąg"
            },

            ["Nogi"] = new List<string>
            {
                "🦵 Przysiad ze sztangą (Squat)",
                "🏗️ Wyciskanie na suwnicy",
                "🍑 Hip Thrust (Unoszenie bioder)",
                "🚶 Wykroki",
                "🦵 Uginanie nóg na maszynie",
                "🦵 Wznosy na łydki"
            },

            ["Barki"] = new List<string>
            {
                "🛡️ Wyciskanie żołnierskie (OHP)",
                "🔩 Wyciskanie hantli siedząc",
                "🦅 Wznosy bokiem",
                "🦅 Wznosy w opadzie",
                "🦋 Odwodzenie ramion na maszynie Butterfly odwrotnie (Rear Delt Fly)",
                "🎯 Face pulls",
                "⬆️ Unoszenie hantli w przód"
            },

            ["Ramiona"] = new List<string>
            {
                "💪 Uginanie ze sztangą (Biceps)",
                "🔨 Uginanie młotkowe",
                "💪 Wyciskanie francuskie",
                "🪢 Prostowanie z linką wyciągu"
            },

            ["Brzuch"] = new List<string>
            {
                "🍫 Allahy (Crunches z linką)",
                "🚲 Rowerek",
                "🛹 Deska (Plank)",
                "🧗 Unoszenie nóg w zwisie"
            }
        };
    }

    private static Dictionary<string, List<string>> LoadCustomExercises()
    {
        var json = Preferences.Default.Get(CustomExercisesKey, string.Empty);

        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, List<string>>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json)
                   ?? new Dictionary<string, List<string>>();
        }
        catch
        {
            return new Dictionary<string, List<string>>();
        }
    }

    private void MergeCustomExercises()
    {
        foreach (var category in _customUserExercises)
        {
            if (!_exercisesDatabase.TryGetValue(category.Key, out var exercises))
                continue;

            foreach (var customExercise in category.Value)
            {
                if (!exercises.Contains(customExercise))
                {
                    exercises.Add(customExercise);
                }
            }
        }
    }

    private static List<WorkoutSet> DeserializeGymSets(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<WorkoutSet>();

        try
        {
            return JsonSerializer.Deserialize<List<WorkoutSet>>(json)
                   ?? new List<WorkoutSet>();
        }
        catch
        {
            return new List<WorkoutSet>();
        }
    }

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

            deleteButton.Clicked += (_, _) =>
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

                if (double.TryParse(
                        value,
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out var parsed))
                {
                    currentSet.Weight = parsed;
                }
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
                {
                    currentSet.Reps = parsed;
                }
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

            inputsGrid.Add(CreateInputBlock("Ciężar (kg)", weightEntry), 0, 0);
            inputsGrid.Add(CreateInputBlock("Powtórzenia", repsEntry), 1, 0);

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

    private static View CreateInputBlock(string labelText, Entry entry)
    {
        return new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label
                {
                    Text = labelText,
                    FontSize = 12,
                    TextColor = Color.FromArgb("#64748B")
                },
                new Border
                {
                    StrokeShape = new RoundRectangle { CornerRadius = 12 },
                    BackgroundColor = Colors.White,
                    Stroke = Color.FromArgb("#E2E8F0"),
                    StrokeThickness = 1,
                    Padding = new Thickness(10, 0),
                    Content = entry
                }
            }
        };
    }

    private void NormalizeSetNumbers()
    {
        for (var i = 0; i < _sets.Count; i++)
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
        var category = CategoryPicker.SelectedIndex >= 0
            ? CategoryPicker.Items[CategoryPicker.SelectedIndex]
            : string.Empty;

        var exercise = ExercisePicker.SelectedIndex >= 0
            ? ExercisePicker.Items[ExercisePicker.SelectedIndex]
            : string.Empty;

        if (string.IsNullOrWhiteSpace(category))
        {
            await ShowValidationAlert("Wybierz partię mięśniową.");
            return;
        }

        if (string.IsNullOrWhiteSpace(exercise) ||
            exercise == AddCustomExerciseItem)
        {
            await ShowValidationAlert("Wybierz ćwiczenie.");
            return;
        }

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
            await ShowValidationAlert(
                "Dodaj przynajmniej jedną serię z ciężarem lub powtórzeniami.");
            return;
        }

        _workout.Category = category;
        _workout.Exercise = exercise;
        _workout.Date = WorkoutDatePicker.Date;
        _workout.Location = string.IsNullOrWhiteSpace(LocationEntry.Text)
            ? null
            : LocationEntry.Text.Trim();
        _workout.GymSetsJson = JsonSerializer.Serialize(validSets);

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