using FitnessTracker.Models;

namespace FitnessTracker.Services;

/// <summary>
/// Obliczenia siłowe: szacowany 1RM, objętość i podpowiedź progresji.
/// </summary>
public static class ProgressionCalculator
{
    // Zakres powtórzeń dla "podwójnej progresji": najpierw dobijamy do górnej granicy,
    // potem podnosimy ciężar i wracamy do dolnej.
    public const int RepRangeLow = 6;
    public const int RepRangeHigh = 10;

    /// <summary>Szacowany 1RM wg wzoru Epleya.</summary>
    public static double EstimateOneRepMax(double weight, int reps)
    {
        if (weight <= 0 || reps <= 0)
            return 0;

        return reps == 1 ? weight : weight * (1.0 + reps / 30.0);
    }

    public static double BestOneRepMax(IEnumerable<WorkoutSet> sets) =>
        sets.Select(s => EstimateOneRepMax(s.Weight, s.Reps)).DefaultIfEmpty(0).Max();

    public static double Volume(IEnumerable<WorkoutSet> sets) =>
        sets.Sum(s => s.Weight * s.Reps);

    /// <summary>
    /// Krok ciężaru: mniejszy dla ćwiczeń izolowanych / lekkich ciężarów.
    /// </summary>
    public static double WeightStep(double currentWeight) => currentWeight < 20 ? 1.0 : 2.5;

    public static ProgressionSuggestion? Suggest(IReadOnlyList<WorkoutSet> lastSets)
    {
        var working = lastSets.Where(s => s.Weight > 0 && s.Reps > 0).ToList();
        if (working.Count == 0)
            return null;

        var topWeight = working.Max(s => s.Weight);
        var topSets = working.Where(s => Math.Abs(s.Weight - topWeight) < 0.01).ToList();
        var minRepsAtTop = topSets.Min(s => s.Reps);

        if (minRepsAtTop >= RepRangeHigh)
        {
            var newWeight = topWeight + WeightStep(topWeight);
            return new ProgressionSuggestion(
                newWeight,
                RepRangeLow,
                $"Wszystkie serie na {topWeight:0.##} kg miały ≥{RepRangeHigh} powt. → spróbuj {newWeight:0.##} kg × {RepRangeLow}.");
        }

        if (minRepsAtTop >= RepRangeLow)
        {
            return new ProgressionSuggestion(
                topWeight,
                minRepsAtTop + 1,
                $"Zostań przy {topWeight:0.##} kg i dołóż powtórzenie: celuj w {minRepsAtTop + 1} w każdej serii.");
        }

        return new ProgressionSuggestion(
            topWeight,
            RepRangeLow,
            $"Ostatnio poniżej {RepRangeLow} powt. na {topWeight:0.##} kg — powtórz ten ciężar i celuj w {RepRangeLow}.");
    }
}

public record ProgressionSuggestion(double Weight, int Reps, string Message);
