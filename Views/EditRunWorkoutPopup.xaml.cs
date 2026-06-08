using CommunityToolkit.Maui.Views;
using FitnessTracker.Models;
using Microsoft.Maui.Controls.Shapes;
using System.Globalization;
using System.Text.Json;

namespace FitnessTracker.Views;

public partial class EditRunWorkoutPopup : Popup
{
    private readonly Workout _workout;
    private readonly Func<Workout, Task> _onSave;
    private readonly List<double> _splits = new();

    public EditRunWorkoutPopup(Workout workout, Func<Workout, Task> onSave)
    {
        InitializeComponent();

        _workout = workout;
        _onSave = onSave;

        WorkoutDatePicker.Date = workout.Date == default ? DateTime.Today : workout.Date;
        DistanceEntry.Text = workout.Distance.ToString("0.##", CultureInfo.InvariantCulture);
        DurationEntry.Text = workout.Duration.ToString("0.##", CultureInfo.InvariantCulture);

        if (!string.IsNullOrWhiteSpace(workout.SplitsJson))
        {
            try
            {
                var savedSplits = JsonSerializer.Deserialize<List<double>>(workout.SplitsJson);
                if (savedSplits != null)
                {
                    _splits.Clear();
                    _splits.AddRange(savedSplits);
                }
            }
            catch
            {
            }
        }

        RenderSplits();
    }

    private void RenderSplits()
    {
        SplitsContainer.Children.Clear();

        for (int i = 0; i < _splits.Count; i++)
        {
            var splitIndex = i;

            var titleLabel = new Label
            {
                Text = $"Split {splitIndex + 1}",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#0F172A"),
                VerticalOptions = LayoutOptions.Center
            };

            var previewLabel = new Label
            {
                Text = _splits[splitIndex] > 0
                    ? $"Tempo: {ConvertSpeedToPace(_splits[splitIndex])}/km"
                    : "Podaj prędkość w km/h",
                FontSize = 12,
                TextColor = Color.FromArgb("#64748B"),
                VerticalOptions = LayoutOptions.Center
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
                if (splitIndex >= 0 && splitIndex < _splits.Count)
                {
                    _splits.RemoveAt(splitIndex);
                    RenderSplits();
                }
            };

            var speedEntry = new Entry
            {
                Text = _splits[splitIndex] > 0
                    ? _splits[splitIndex].ToString("0.##", CultureInfo.InvariantCulture)
                    : string.Empty,
                Placeholder = "np. 13.6",
                Keyboard = Keyboard.Numeric,
                TextColor = Color.FromArgb("#0F172A"),
                BackgroundColor = Colors.Transparent,
                HeightRequest = 44,
                HorizontalTextAlignment = TextAlignment.Center
            };

            speedEntry.TextChanged += (_, e) =>
            {
                var value = (e.NewTextValue ?? string.Empty).Replace(',', '.');

                if (string.IsNullOrWhiteSpace(value))
                {
                    _splits[splitIndex] = 0;
                    previewLabel.Text = "Podaj prędkość w km/h";
                    return;
                }

                if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
                {
                    _splits[splitIndex] = parsed;
                    previewLabel.Text = $"Tempo: {ConvertSpeedToPace(parsed)}/km";
                }
                else
                {
                    previewLabel.Text = "Niepoprawna wartość";
                }
            };

            var topGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                ColumnSpacing = 8
            };

            topGrid.Add(new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    titleLabel,
                    previewLabel
                }
            }, 0, 0);

            topGrid.Add(deleteButton, 1, 0);

            var inputBorder = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                BackgroundColor = Colors.White,
                Stroke = Color.FromArgb("#E2E8F0"),
                StrokeThickness = 1,
                Padding = new Thickness(10, 0),
                Content = speedEntry
            };

            var inputBlock = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label
                    {
                        Text = "Prędkość (km/h)",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#64748B")
                    },
                    inputBorder
                }
            };

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
                        topGrid,
                        inputBlock
                    }
                }
            };

            SplitsContainer.Children.Add(card);
        }
    }

    private void OnAddSplitClicked(object sender, EventArgs e)
    {
        _splits.Add(0);
        RenderSplits();
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        Close();
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var distanceText = (DistanceEntry.Text ?? string.Empty).Replace(',', '.');
        var durationText = (DurationEntry.Text ?? string.Empty).Replace(',', '.');

        if (!double.TryParse(distanceText, NumberStyles.Any, CultureInfo.InvariantCulture, out var distance) || distance <= 0)
        {
            await ShowValidationAlert("Podaj poprawny dystans.");
            return;
        }

        if (!double.TryParse(durationText, NumberStyles.Any, CultureInfo.InvariantCulture, out var duration) || duration <= 0)
        {
            await ShowValidationAlert("Podaj poprawny czas.");
            return;
        }

        var validSplits = _splits
            .Where(x => x > 0)
            .ToList();

        _workout.Date = WorkoutDatePicker.Date;
        _workout.Distance = distance;
        _workout.Duration = duration;
        _workout.SplitsJson = JsonSerializer.Serialize(validSplits);

        if (_onSave != null)
            await _onSave(_workout);

        Close();
    }

    private static string ConvertSpeedToPace(double speedKmH)
    {
        if (speedKmH <= 0)
            return "-";

        var totalMinutesPerKm = 60.0 / speedKmH;
        var minutes = (int)Math.Floor(totalMinutesPerKm);
        var seconds = (int)Math.Round((totalMinutesPerKm - minutes) * 60);

        if (seconds == 60)
        {
            minutes++;
            seconds = 0;
        }

        return $"{minutes}:{seconds:00}";
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