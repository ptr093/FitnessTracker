using System.Diagnostics;

namespace FitnessTracker;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new AppShell();

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            WriteCrashLog("AppDomain.CurrentDomain.UnhandledException",
                args.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            WriteCrashLog("TaskScheduler.UnobservedTaskException",
                args.Exception);

            args.SetObserved();
        };
    }

    private static void WriteCrashLog(string source, Exception? exception)
    {
        try
        {
            var filePath = Path.Combine(
                FileSystem.AppDataDirectory,
                "crash.log");

            var message =
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                $"Source: {source}\n" +
                $"{exception}\n\n";

            File.AppendAllText(filePath, message);
        }
        catch
        {
            // Logger nie może spowodować kolejnego crasha.
        }
    }
}