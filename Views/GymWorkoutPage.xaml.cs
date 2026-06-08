// Plik: Views/AddWorkoutPage.xaml.cs
using FitnessTracker.ViewModels;

namespace FitnessTracker.Views;

public partial class GymWorkoutPage : ContentPage
{
    public GymWorkoutPage(GymWorkoutViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}