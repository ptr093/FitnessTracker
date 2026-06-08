using SQLite;
using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FitnessTracker.Models
{
    public class Workout
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;

        // "Siłownia" lub "Bieganie"
        public string Type { get; set; } = string.Empty;

        // Id aktywności ze Stravy - do unikania duplikatów
        public long? StravaActivityId { get; set; }

        // ==========================================
        // POLA DLA BIEGANIA
        // ==========================================
        public double Distance { get; set; }
        public double Duration { get; set; }
        public string SplitsJson { get; set; } = "[]";

        // ==========================================
        // POLA DLA SIŁOWNI
        // ==========================================
        public string Category { get; set; } = string.Empty;
        public string Exercise { get; set; } = string.Empty;
        public string GymSetsJson { get; set; } = "[]";

        [Ignore]
        public string Summary
        {
            get
            {
                if (Type == "Bieganie")
                    return $"{Distance} km w {Duration} min";

                return $"{Exercise} ({Category})";
            }
        }
    }

    public partial class WorkoutSet : ObservableObject
    {
        [ObservableProperty]
        private int setNumber;

        [ObservableProperty]
        private double weight;

        [ObservableProperty]
        private int reps;
    }

    public class RunSplit
    {
        public int Kilometer { get; set; }
        public string Pace { get; set; } = string.Empty;
    }
}