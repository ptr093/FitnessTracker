using CommunityToolkit.Maui;
using FitnessTracker.Services;
using FitnessTracker.ViewModels;
using FitnessTracker.Views;
using Microsoft.Extensions.Logging;

namespace FitnessTracker
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder()
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    // Ikony interfejsu (Google Material Icons Round, licencja Apache 2.0)
                    fonts.AddFont("MaterialIconsRound-Regular.otf", "MaterialSymbols");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            builder.Services.AddSingleton<HttpClient>();

            // Nasze serwisy
            builder.Services.AddSingleton<DatabaseService>();
            builder.Services.AddSingleton<ExerciseCatalogService>();
            builder.Services.AddSingleton<BackupService>();
            builder.Services.AddSingleton<FitImportService>();
            builder.Services.AddSingleton<GarminApiService>();

            builder.Services.AddTransient<ProgressViewModel>();
            builder.Services.AddTransient<DataSyncViewModel>();
            builder.Services.AddTransient<ProgressPage>();
            builder.Services.AddTransient<DataSyncPage>();


            builder.Services.AddTransient<GymWorkoutViewModel>();
            builder.Services.AddTransient<HistoryViewModel>();
            builder.Services.AddTransient<RecordsViewModel>();
            builder.Services.AddTransient<DashboardViewModel>();
            builder.Services.AddTransient<RunWorkoutViewModel>();
            builder.Services.AddTransient<StatsViewModel>();
            builder.Services.AddTransient<ManageExercisesViewModel>();
            builder.Services.AddTransient<WorkoutTemplatesViewModel>();
            builder.Services.AddTransient<EditWorkoutTemplateViewModel>();
            builder.Services.AddTransient<TemplateWorkoutViewModel>();


            builder.Services.AddTransient<DashboardPage>();
            builder.Services.AddTransient<HistoryPage>();
            builder.Services.AddTransient<RecordsPage>();
            builder.Services.AddTransient<GymWorkoutPage>();
            builder.Services.AddTransient<RunWorkoutPage>();
            builder.Services.AddTransient<EditGymWorkoutPopup>();
            builder.Services.AddTransient<EditRunWorkoutPopup>();
            builder.Services.AddTransient<StatsPage>();
            builder.Services.AddTransient<ManageExercisesPage>();

            builder.Services.AddTransient<WorkoutTemplatesPage>();
            builder.Services.AddTransient<EditWorkoutTemplatePage>();
            builder.Services.AddTransient<TemplateWorkoutPage>();

            return builder.Build();
        }
    }
}