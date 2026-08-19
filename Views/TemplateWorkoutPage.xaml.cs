using FitnessTracker.ViewModels;

namespace FitnessTracker.Views;

public partial class TemplateWorkoutPage : ContentPage
{
    public TemplateWorkoutPage(TemplateWorkoutViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}