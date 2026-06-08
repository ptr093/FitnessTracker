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
        }
    }
}
