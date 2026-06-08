using FitnessTracker.ViewModels;

namespace FitnessTracker.Views;

public partial class RunWorkoutPage : ContentPage
{
    public RunWorkoutPage(RunWorkoutViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}