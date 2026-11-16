using FitnessTracker.ViewModels;
using FitnessTracker.Views;

namespace FitnessTracker
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("GymWorkoutPage", typeof(GymWorkoutPage));
            Routing.RegisterRoute("RunWorkoutPage", typeof(RunWorkoutPage));
            Routing.RegisterRoute("HistoryPage", typeof(HistoryPage));
            Routing.RegisterRoute("records", typeof(RecordsPage));
            Routing.RegisterRoute("StatsPage", typeof(StatsPage));
            Routing.RegisterRoute("ManageExPage", typeof(ManageExercisesPage));
            Routing.RegisterRoute("WorkoutTemplatesPage", typeof(WorkoutTemplatesPage));
            Routing.RegisterRoute(nameof(ProgressPage), typeof(ProgressPage));
            Routing.RegisterRoute(nameof(DataSyncPage), typeof(DataSyncPage));
         

            Routing.RegisterRoute(
                nameof(EditWorkoutTemplatePage),
                typeof(EditWorkoutTemplatePage));

            Routing.RegisterRoute(
                nameof(TemplateWorkoutPage),
                typeof(TemplateWorkoutPage));



        }
    }
}
