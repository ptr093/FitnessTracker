using FitnessTracker.ViewModels;

namespace FitnessTracker.Views;

public partial class EditWorkoutTemplatePage : ContentPage
{
    public EditWorkoutTemplatePage(EditWorkoutTemplateViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}