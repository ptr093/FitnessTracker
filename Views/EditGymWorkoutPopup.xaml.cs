using CommunityToolkit.Maui.Views;
using FitnessTracker.Models;
using Microsoft.Maui.Controls.Shapes;
using System.Globalization;
using System.Text.Json;

namespace FitnessTracker.Views;

public partial class EditGymWorkoutPopup : Popup
{
    private readonly Workout _workout;
    private readonly Func<Workout, Task> _onSave;
    private readonly List<WorkoutSet> _sets;

    public EditGymWorkoutPopup(Workout workout, Func<Workout, Task> onSave)
    {
        InitializeComponent();

        _workout = workout;
        _onSave = onSave;

        CategoryEntry.Text = workout.Category;
        ExerciseEntry.Text = workout.Exercise;
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
        var category = CategoryEntry.Text?.Trim() ?? string.Empty;
        var exercise = ExerciseEntry.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(category))
        {
            await ShowValidationAlert("Uzupełnij partię mięśniową.");
            return;
        }

        if (string.IsNullOrWhiteSpace(exercise))
        {
            await ShowValidationAlert("Uzupełnij nazwę ćwiczenia.");
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