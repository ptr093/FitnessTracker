using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessTracker.Models;
using FitnessTracker.Services;

namespace FitnessTracker.ViewModels
{
    public partial class RunWorkoutViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        public RunWorkoutViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;

            // Na start dodajemy pierwszy kilometr
            Splits.Add(new RunSplit
            {
                Kilometer = 1,
                Pace = "5:00"
            });
        }

        [ObservableProperty]
        private double runDistance;

        [ObservableProperty]
        private double runTime;

        public ObservableCollection<RunSplit> Splits { get; } = new();


        [RelayCommand]
        private void RemoveSplit(RunSplit split)
        {
            if (split == null)
                return;

            if (Splits.Contains(split))
            {
                Splits.Remove(split);

                for (int i = 0; i < Splits.Count; i++)
                {
                    Splits[i].Kilometer = i + 1;
                }

                if (Splits.Count == 0)
                {
                    Splits.Add(new RunSplit
                    {
                        Kilometer = 1,
                        Pace = "5:00"
                    });
                }
            }
        }

        [RelayCommand]
        private void AddSplit()
        {
            Splits.Add(new RunSplit
            {
                Kilometer = Splits.Count + 1,
                Pace = "5:00"
            });
        }

        [RelayCommand]
        private async Task SaveWorkoutAsync()
        {
            if (RunDistance <= 0)
            {
                if (App.Current.MainPage != null)
                {
                    await App.Current.MainPage.DisplayAlert("Błąd", "Podaj dystans biegu.", "OK");
                }
                return;
            }

            if (RunTime <= 0)
            {
                if (App.Current.MainPage != null)
                {
                    await App.Current.MainPage.DisplayAlert("Błąd", "Podaj całkowity czas biegu.", "OK");
                }
                return;
            }

            var workout = new Workout
            {
                Type = "Bieganie",
                Distance = RunDistance,
                Duration = RunTime,
                SplitsJson = JsonSerializer.Serialize(Splits),
                Date = DateTime.Now
            };

            await _databaseService.SaveWorkoutAsync(workout);

            RunDistance = 0;
            RunTime = 0;
            Splits.Clear();

            Splits.Add(new RunSplit
            {
                Kilometer = 1,
                Pace = "5:00"
            });

            if (App.Current.MainPage != null)
            {
                await App.Current.MainPage.DisplayAlert("Sukces", "Bieg został zapisany.", "OK");
            }

            await Shell.Current.GoToAsync("..");
        }


    }


}