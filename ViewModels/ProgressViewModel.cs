using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessTracker.Models;
using FitnessTracker.Services;
using FitnessTracker.Views.Charts;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FitnessTracker.ViewModels;

public partial class ProgressViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private List<Workout> _gymWorkouts = new();

    public const string MetricOneRepMax = "Szacowany 1RM";
    public const string MetricMaxWeight = "Maks. ciężar";
    public const string MetricVolume = "Objętość";

    public ObservableCollection<string> Exercises { get; } = new();
    public ObservableCollection<string> Metrics { get; } = new() { MetricOneRepMax, MetricMaxWeight, MetricVolume };
    public ObservableCollection<WeeklyCategoryVolume> WeeklyVolume { get; } = new();

    public LineChartDrawable Chart { get; } = new();

    /// <summary>Widok podpina tu odświeżenie GraphicsView.</summary>
    public event EventHandler? ChartInvalidated;

    [ObservableProperty]
    private string? selectedExercise;

    [ObservableProperty]
    private string selectedMetric = MetricOneRepMax;

    [ObservableProperty]
    private string bestValueText = "—";

    [ObservableProperty]
    private string changeText = "—";

    [ObservableProperty]
    private Color changeColor = FitnessTracker.Services.ThemeService.Get("TextMuted");

    [ObservableProperty]
    private string sessionsText = "0";

    [ObservableProperty]
    private string weekRangeText = string.Empty;

    [ObservableProperty]
    private bool isEmpty;

    public ProgressViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var all = await _databaseService.GetWorkoutsAsync();
        _gymWorkouts = all.Where(w => w.Type == "Siłownia" && !string.IsNullOrWhiteSpace(w.Exercise)).ToList();
        IsEmpty = _gymWorkouts.Count == 0;

        var previous = SelectedExercise;
        Exercises.Clear();

        // Najczęściej / ostatnio robione na górze
        foreach (var name in _gymWorkouts
                     .GroupBy(w => w.Exercise)
                     .OrderByDescending(g => g.Max(w => w.Date))
                     .Select(g => g.Key))
        {
            Exercises.Add(name);
        }

        SelectedExercise = previous != null && Exercises.Contains(previous)
            ? previous
            : Exercises.FirstOrDefault();

        BuildChart();
        BuildWeeklyVolume();
    }

    partial void OnSelectedExerciseChanged(string? value) => BuildChart();
    partial void OnSelectedMetricChanged(string value) => BuildChart();

    private void BuildChart()
    {
        var points = new List<ChartPoint>();

        if (!string.IsNullOrEmpty(SelectedExercise))
        {
            // Jeden punkt na dzień – najlepszy wynik z danego dnia (kilka zapisów tego samego ćwiczenia łączymy)
            points = _gymWorkouts
                .Where(w => w.Exercise == SelectedExercise)
                .GroupBy(w => w.Date.Date)
                .Select(day =>
                {
                    var sets = day.SelectMany(w => DatabaseService.GetOrderedSets(w)).ToList();
                    var value = SelectedMetric switch
                    {
                        MetricMaxWeight => sets.Select(s => s.Weight).DefaultIfEmpty(0).Max(),
                        MetricVolume => ProgressionCalculator.Volume(sets),
                        _ => ProgressionCalculator.BestOneRepMax(sets)
                    };
                    return new ChartPoint(day.Key, Math.Round(value, 1));
                })
                .Where(p => p.Value > 0)
                .OrderBy(p => p.Date)
                .ToList();
        }

        Chart.Points = points;
        Chart.Unit = "kg";
        SessionsText = points.Count.ToString(CultureInfo.CurrentCulture);

        if (points.Count == 0)
        {
            BestValueText = "—";
            ChangeText = "—";
            ChangeColor = FitnessTracker.Services.ThemeService.Get("TextMuted");
        }
        else
        {
            BestValueText = $"{points.Max(p => p.Value):0.#} kg";

            var first = points[0].Value;
            var last = points[^1].Value;
            if (points.Count > 1 && first > 0)
            {
                var pct = (last - first) / first * 100;
                ChangeText = $"{(pct >= 0 ? "+" : "")}{pct:0.#}%";
                ChangeColor = pct >= 0 ? FitnessTracker.Services.ThemeService.Get("Green") : FitnessTracker.Services.ThemeService.Get("Red");
            }
            else
            {
                ChangeText = "—";
                ChangeColor = FitnessTracker.Services.ThemeService.Get("TextMuted");
            }
        }

        ChartInvalidated?.Invoke(this, EventArgs.Empty);
    }

    private void BuildWeeklyVolume()
    {
        WeeklyVolume.Clear();

        var thisWeekStart = StartOfWeek(DateTime.Today);
        var lastWeekStart = thisWeekStart.AddDays(-7);
        WeekRangeText = $"Ten tydzień od {thisWeekStart:dd.MM} vs poprzedni";

        var recent = _gymWorkouts.Where(w => w.Date >= lastWeekStart).ToList();

        var rows = recent
            .GroupBy(w => string.IsNullOrWhiteSpace(w.Category) ? "Inne" : w.Category)
            .Select(g =>
            {
                var thisWeek = g.Where(w => w.Date >= thisWeekStart).SelectMany(DatabaseService.GetOrderedSets).ToList();
                var lastWeek = g.Where(w => w.Date < thisWeekStart).SelectMany(DatabaseService.GetOrderedSets).ToList();
                return new WeeklyCategoryVolume(
                    g.Key,
                    thisWeek.Count,
                    lastWeek.Count,
                    ProgressionCalculator.Volume(thisWeek));
            })
            .OrderByDescending(r => r.SetsThisWeek)
            .ThenBy(r => r.Category)
            .ToList();

        var maxSets = Math.Max(1, rows.Select(r => Math.Max(r.SetsThisWeek, r.SetsLastWeek)).DefaultIfEmpty(1).Max());
        foreach (var row in rows)
        {
            row.BarProgress = row.SetsThisWeek / (double)maxSets;
            WeeklyVolume.Add(row);
        }
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }
}

public class WeeklyCategoryVolume
{
    public WeeklyCategoryVolume(string category, int setsThisWeek, int setsLastWeek, double volumeThisWeek)
    {
        Category = category;
        SetsThisWeek = setsThisWeek;
        SetsLastWeek = setsLastWeek;
        VolumeThisWeek = volumeThisWeek;
    }

    public string Category { get; }
    public int SetsThisWeek { get; }
    public int SetsLastWeek { get; }
    public double VolumeThisWeek { get; }
    public double BarProgress { get; set; }

    public string SetsText => $"{SetsThisWeek} serii";
    public string CompareText
    {
        get
        {
            var diff = SetsThisWeek - SetsLastWeek;
            var sign = diff > 0 ? "+" : "";
            return $"poprz. {SetsLastWeek} ({sign}{diff}) · {VolumeThisWeek:0} kg";
        }
    }

    // Wskazówka: ~10–20 serii tygodniowo na partię to typowy zakres na hipertrofię
    public string Hint => SetsThisWeek switch
    {
        0 => "brak w tym tygodniu",
        < 10 => "poniżej 10 serii",
        <= 20 => "w zakresie 10–20",
        _ => "dużo (>20)"
    };
}
