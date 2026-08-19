using FitnessTracker.ViewModels;

namespace FitnessTracker.Views;

public partial class WorkoutTemplatesPage : ContentPage
{
    private readonly WorkoutTemplatesViewModel _viewModel;

    public WorkoutTemplatesPage(WorkoutTemplatesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}