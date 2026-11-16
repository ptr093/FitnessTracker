using FitnessTracker.ViewModels;

namespace FitnessTracker.Views;

public partial class ProgressPage : ContentPage
{
    private readonly ProgressViewModel _viewModel;

    public ProgressPage(ProgressViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        ChartView.Drawable = viewModel.Chart;
        viewModel.ChartInvalidated += (_, _) => ChartView.Invalidate();
        FitnessTracker.Services.ThemeService.ThemeChanged += OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ChartView.Invalidate();

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        FitnessTracker.Services.ThemeService.ThemeChanged -= OnThemeChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
