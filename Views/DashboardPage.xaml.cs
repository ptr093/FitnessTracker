using FitnessTracker.ViewModels;

namespace FitnessTracker.Views;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _viewModel;

    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Odświeżamy po każdym powrocie, np. po zapisaniu treningu
        await _viewModel.LoadSummaryAsync();
    }
}
