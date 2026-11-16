using FitnessTracker.ViewModels;

namespace FitnessTracker.Views;

public partial class DataSyncPage : ContentPage
{
    private readonly DataSyncViewModel _viewModel;

    public DataSyncPage(DataSyncViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
}
