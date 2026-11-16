using FitnessTracker.ViewModels;

namespace FitnessTracker.Views;

public partial class GymWorkoutPage : ContentPage
{
    private readonly GymWorkoutViewModel _viewModel;

    public GymWorkoutPage(GymWorkoutViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.OnPageDisappearing();
    }
}
