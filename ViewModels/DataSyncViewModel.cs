using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessTracker.Services;

namespace FitnessTracker.ViewModels;

public partial class DataSyncViewModel : ObservableObject
{
    private readonly FitImportService _fitImportService;
    private readonly BackupService _backupService;
    private readonly GarminApiService _garminApiService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    [ObservableProperty]
    private string? statusText;

    public bool IsNotBusy => !IsBusy;

    // ---------- Garmin Personal API ----------
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoGarminKey))]
    private bool hasGarminKey;

    public bool HasNoGarminKey => !HasGarminKey;

    [ObservableProperty]
    private string? garminApiKeyInput;

    public List<GarminRangeOption> GarminRanges { get; } = new()
    {
        new("Ostatnie 7 dni", 7),
        new("Ostatnie 30 dni", 30),
        new("Ostatnie 3 miesiące", 90),
        new("Ostatnie 6 miesięcy", 182),
        new("Ostatni rok", 365),
        new("Ostatnie 2 lata", 730),
        new("Ostatnie 5 lat", 1826)
    };

    [ObservableProperty]
    private GarminRangeOption? selectedGarminRange;

    partial void OnSelectedGarminRangeChanged(GarminRangeOption? value)
    {
        if (value != null)
            Preferences.Default.Set("GarminImportDays", value.Days);
    }

    private CancellationTokenSource? _garminCts;

    [ObservableProperty]
    private bool canCancelGarmin;

    public DataSyncViewModel(
        FitImportService fitImportService,
        BackupService backupService,
        GarminApiService garminApiService)
    {
        _fitImportService = fitImportService;
        _backupService = backupService;
        _garminApiService = garminApiService;

        var savedDays = Preferences.Default.Get("GarminImportDays", 30);
        SelectedGarminRange = GarminRanges.FirstOrDefault(r => r.Days == savedDays) ?? GarminRanges[1];
    }

    public async Task InitializeAsync()
    {
        try
        {
            HasGarminKey = await _garminApiService.HasApiKeyAsync();
        }
        catch
        {
            HasGarminKey = false;
        }
    }

    [RelayCommand]
    private Task SaveGarminKeyAsync() => RunAsync("Garmin", async () =>
    {
        if (string.IsNullOrWhiteSpace(GarminApiKeyInput))
            return "Wklej klucz z connect.garmin.com.";

        StatusText = "Sprawdzanie klucza w Garminie…";
        await _garminApiService.SaveApiKeyAsync(GarminApiKeyInput);

        GarminApiKeyInput = null; // nie trzymamy klucza w polu po zapisie
        HasGarminKey = true;
        return "Klucz działa i został zapisany bezpiecznie na urządzeniu. Możesz pobrać treningi.";
    });

    [RelayCommand]
    private async Task RemoveGarminKeyAsync()
    {
        var page = Shell.Current?.CurrentPage;
        if (page != null && !await page.DisplayAlert(
                "Usuń klucz Garmina",
                "Klucz zostanie usunięty z tego urządzenia. Zaimportowane treningi zostają.",
                "Usuń", "Anuluj"))
            return;

        _garminApiService.RemoveApiKey();
        HasGarminKey = false;
        StatusText = "Klucz Garmina usunięty z urządzenia.";
    }

    [RelayCommand]
    private async Task ImportGarminAsync()
    {
        var days = SelectedGarminRange?.Days ?? 30;

        // Duży zakres = dużo pobrań; uprzedzamy, że może to potrwać
        if (days > 90 && Shell.Current?.CurrentPage is { } page &&
            !await page.DisplayAlert(
                "Duży zakres",
                $"Pobranie: {SelectedGarminRange?.Label.ToLowerInvariant()}. Każda aktywność to osobny plik, więc może to potrwać kilka minut. " +
                "Możesz przerwać w dowolnym momencie – pobrane zostaną zapisane, a kolejna synchronizacja dokończy resztę.",
                "Pobierz", "Anuluj"))
            return;

        _garminCts = new CancellationTokenSource();
        CanCancelGarmin = true;

        try
        {
            await RunAsync("Garmin", async () =>
            {
                var progress = new Progress<string>(text => StatusText = text);
                var result = await _garminApiService.ImportRecentAsync(days, progress: progress,
                    cancellationToken: _garminCts.Token);
                return result.ToMessage();
            });
        }
        finally
        {
            CanCancelGarmin = false;
            _garminCts.Dispose();
            _garminCts = null;
        }
    }

    [RelayCommand]
    private void CancelGarmin()
    {
        _garminCts?.Cancel();
        StatusText = "Przerywanie po bieżącej aktywności…";
    }

    [RelayCommand]
    private Task ImportFitAsync() => RunAsync("Garmin .FIT", async () =>
    {
        StatusText = "Wybierz pliki .FIT…";
        var result = await _fitImportService.PickAndImportAsync();
        return result?.ToMessage();
    });

    [RelayCommand]
    private Task ExportJsonAsync() => RunAsync("Kopia zapasowa", async () =>
    {
        var path = await _backupService.ExportJsonAsync();
        return path == null ? null : $"Zapisano kopię:\n{path}";
    });

    [RelayCommand]
    private Task ExportCsvAsync() => RunAsync("Eksport CSV", async () =>
    {
        var path = await _backupService.ExportCsvAsync();
        return path == null ? null : $"Zapisano plik CSV:\n{path}";
    });

    [RelayCommand]
    private async Task ImportJsonAsync()
    {
        var page = Shell.Current?.CurrentPage;
        if (page != null && !await page.DisplayAlert(
                "Przywracanie kopii",
                "Dane z pliku zostaną DODANE do obecnych. Treningi, które już są w bazie, zostaną pominięte. Kontynuować?",
                "Tak", "Anuluj"))
            return;

        await RunAsync("Przywracanie kopii", async () =>
        {
            var result = await _backupService.ImportJsonAsync();
            return result == null
                ? null
                : $"Dodano treningów: {result.WorkoutsAdded}\nPominięto (duplikaty): {result.WorkoutsSkipped}\n" +
                  $"Szablony: {result.TemplatesAdded}\nWłasne ćwiczenia: {result.CustomExercisesAdded}";
        });
    }

    public record GarminRangeOption(string Label, int Days)
    {
        public override string ToString() => Label;
    }

    private async Task RunAsync(string title, Func<Task<string?>> action)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var message = await action();
            StatusText = message;

            if (message != null && Shell.Current?.CurrentPage is { } page)
                await page.DisplayAlert(title, message, "OK");
        }
        catch (Exception ex)
        {
            StatusText = $"Błąd: {ex.Message}";
            if (Shell.Current?.CurrentPage is { } page)
                await page.DisplayAlert($"{title} – błąd", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
