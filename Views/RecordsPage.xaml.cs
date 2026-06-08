using FitnessTracker.ViewModels;

namespace FitnessTracker.Views;

public partial class RecordsPage : ContentPage
{
    private readonly RecordsViewModel _viewModel;

    public RecordsPage(RecordsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadDataCommand.ExecuteAsync(null);
    }
}