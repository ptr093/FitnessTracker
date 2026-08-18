using FitnessTracker.ViewModels;

namespace FitnessTracker.Views;

public partial class ManageExercisesPage : ContentPage
{
    public ManageExercisesPage(ManageExercisesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}