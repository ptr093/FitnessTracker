using FitnessTracker.Resources.Themes;

namespace FitnessTracker.Services;

public enum ThemeMode
{
    System,
    Light,
    Dark
}

/// <summary>
/// Przełącza słownik kolorów (LightTheme / DarkTheme) w zasobach aplikacji.
/// Widoki korzystają z {DynamicResource}, więc zmiana jest natychmiastowa.
/// </summary>
public static class ThemeService
{
    private const string PreferenceKey = "AppThemeMode";
    private static ResourceDictionary? _current;

    public static event EventHandler? ThemeChanged;

    public static ThemeMode Mode
    {
        get => Enum.TryParse<ThemeMode>(Preferences.Default.Get(PreferenceKey, nameof(ThemeMode.System)), out var m)
            ? m
            : ThemeMode.System;
        set
        {
            Preferences.Default.Set(PreferenceKey, value.ToString());
            Apply();
        }
    }

    public static bool IsDark
    {
        get
        {
            var app = Application.Current;
            return Mode switch
            {
                ThemeMode.Dark => true,
                ThemeMode.Light => false,
                _ => app?.RequestedTheme == AppTheme.Dark
            };
        }
    }

    public static void Initialize(Application app)
    {
        app.RequestedThemeChanged += (_, _) =>
        {
            if (Mode == ThemeMode.System)
                Apply();
        };

        Apply();
    }

    public static void Apply()
    {
        var app = Application.Current;
        if (app == null)
            return;

        var dark = IsDark;
        ResourceDictionary next = dark ? new DarkTheme() : new LightTheme();

        var merged = app.Resources.MergedDictionaries;
        if (_current != null)
            merged.Remove(_current);
        merged.Add(next);
        _current = next;

        // Systemowe elementy (okna dialogowe, klawiatura, pasek stanu) w tym samym motywie
        app.UserAppTheme = dark ? AppTheme.Dark : AppTheme.Light;

        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Kolor z aktywnego motywu – do użycia w kodzie C# (wykresy, kolory dynamiczne).</summary>
    public static Color Get(string key, Color? fallback = null)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color)
            return color;

        return fallback ?? Colors.Gray;
    }

    /// <summary>Kolejny tryb: system → jasny → ciemny → system.</summary>
    public static ThemeMode Next() => Mode switch
    {
        ThemeMode.System => ThemeMode.Light,
        ThemeMode.Light => ThemeMode.Dark,
        _ => ThemeMode.System
    };
}
