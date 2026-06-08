using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Graphics;

namespace FitnessTracker.Models
{
    public partial class HistoryDayItem : ObservableObject
    {
        [ObservableProperty]
        private DateTime date;

        [ObservableProperty]
        private string dayLabel = string.Empty;

        [ObservableProperty]
        private Color backgroundColor = Colors.White;

        [ObservableProperty]
        private Color textColor = Colors.Black;
    }
}