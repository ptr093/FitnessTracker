using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;

namespace FitnessTracker.ViewModels;

public partial class StatsViewModel : ObservableObject
{
    public ObservableCollection<string> GenderOptions { get; } = new()
    {
        "Mężczyzna",
        "Kobieta"
    };

    [ObservableProperty]
    private string selectedGender = "Mężczyzna";

    [ObservableProperty]
    private double weight;

    [ObservableProperty]
    private double height;

    [ObservableProperty]
    private double liftedWeight;

    [ObservableProperty]
    private int reps = 1;

    [ObservableProperty]
    private double estimatedOneRepMax;

    [ObservableProperty]
    private double wilksScore;

    [ObservableProperty]
    private double bmi;

    [ObservableProperty]
    private string bmiCategory = "Brak danych";

    [ObservableProperty]
    private Color bmiColor = Colors.Gray;

    public StatsViewModel()
    {
        LoadPreferences();
        CalculateAll();
    }

    partial void OnSelectedGenderChanged(string value)
    {
        SavePreferences();
        CalculateAll();
    }

    partial void OnWeightChanged(double value)
    {
        SavePreferences();
        CalculateAll();
    }

    partial void OnHeightChanged(double value)
    {
        SavePreferences();
        CalculateAll();
    }

    partial void OnLiftedWeightChanged(double value)
    {
        SavePreferences();
        CalculateAll();
    }

    partial void OnRepsChanged(int value)
    {
        SavePreferences();
        CalculateAll();
    }

    private void CalculateAll()
    {
        CalculateBmi();
        CalculateWilks();
    }

    private void CalculateBmi()
    {
        if (Weight <= 0 || Height <= 0)
        {
            Bmi = 0;
            BmiCategory = "Brak danych";
            BmiColor = Colors.Gray;
            return;
        }

        double heightMeters = Height / 100.0;
        Bmi = Math.Round(Weight / (heightMeters * heightMeters), 2);

        if (Bmi < 18.5)
        {
            BmiCategory = "Niedowaga";
            BmiColor = Color.FromArgb("#3B82F6");
        }
        else if (Bmi < 25)
        {
            BmiCategory = "W normie";
            BmiColor = Color.FromArgb("#10B981");
        }
        else if (Bmi < 30)
        {
            BmiCategory = "Nadwaga";
            BmiColor = Color.FromArgb("#F59E0B");
        }
        else
        {
            BmiCategory = "Otyłość";
            BmiColor = Color.FromArgb("#EF4444");
        }
    }

    private void CalculateWilks()
    {
        if (Weight <= 0 || LiftedWeight <= 0 || Reps <= 0)
        {
            EstimatedOneRepMax = 0;
            WilksScore = 0;
            return;
        }

        if (Reps == 1)
        {
            EstimatedOneRepMax = LiftedWeight;
        }
        else
        {
            EstimatedOneRepMax = LiftedWeight * (1.0 + (Reps / 30.0));
        }

        bool isMale = SelectedGender == "Mężczyzna";
        double coeff = CalculateWilksCoefficient(Weight, isMale);

        WilksScore = Math.Round(EstimatedOneRepMax * coeff, 2);
    }

    private double CalculateWilksCoefficient(double bw, bool isMale)
    {
        double a, b, c, d, e, f;

        if (isMale)
        {
            a = -216.0475144;
            b = 16.2606339;
            c = -0.002388645;
            d = -0.00113732;
            e = 7.01863E-06;
            f = -1.291E-08;
        }
        else
        {
            a = 594.31747775582;
            b = -27.23842536447;
            c = 0.82112226871;
            d = -0.00930733913;
            e = 4.731582E-05;
            f = -9.054E-08;
        }

        double denominator =
            a + (b * bw) +
            (c * Math.Pow(bw, 2)) +
            (d * Math.Pow(bw, 3)) +
            (e * Math.Pow(bw, 4)) +
            (f * Math.Pow(bw, 5));

        return 500.0 / denominator;
    }

    private void LoadPreferences()
    {
        Weight = Preferences.Get("BodyWeight", 0.0);
        Height = Preferences.Get("BodyHeight", 0.0);
        LiftedWeight = Preferences.Get("WilksLiftedWeight", 0.0);

        int savedReps = Preferences.Get("WilksReps", 1);
        Reps = savedReps > 0 ? savedReps : 1;

        var savedGender = Preferences.Get("SelectedGender", "Mężczyzna");
        SelectedGender = GenderOptions.Contains(savedGender) ? savedGender : "Mężczyzna";
    }

    private void SavePreferences()
    {
        Preferences.Set("BodyWeight", Weight);
        Preferences.Set("BodyHeight", Height);
        Preferences.Set("WilksLiftedWeight", LiftedWeight);
        Preferences.Set("WilksReps", Reps);
        Preferences.Set("SelectedGender", SelectedGender);
    }
}