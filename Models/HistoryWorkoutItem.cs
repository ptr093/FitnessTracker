using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Graphics;
using System.Collections.ObjectModel;

namespace FitnessTracker.Models
{
    public partial class HistoryWorkoutItem : ObservableObject
    {
        [ObservableProperty]
        private int workoutId;

        [ObservableProperty]
        private string headerTitle = string.Empty;

        [ObservableProperty]
        private string headerSubtitle = string.Empty;

        [ObservableProperty]
        private string badgeText = string.Empty;

        [ObservableProperty]
        private Color badgeBackground = Colors.LightGray;

        [ObservableProperty]
        private Color badgeTextColor = Colors.Black;

        [ObservableProperty]
        private ObservableCollection<string> detailLines = new();

        [ObservableProperty]
        private bool isExpanded;

        public string ExpandButtonText => IsExpanded ? "Ukryj szczegóły" : "Pokaż szczegóły";

        partial void OnIsExpandedChanged(bool value)
        {
            OnPropertyChanged(nameof(ExpandButtonText));
        }
    }
}