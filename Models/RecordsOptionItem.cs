using Microsoft.Maui.Graphics;

namespace FitnessTracker.Models;

public class RecordsOptionItem
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;

    public Color BackgroundColor { get; set; } = FitnessTracker.Services.ThemeService.Get("Surface");
    public Color BorderColor { get; set; } = FitnessTracker.Services.ThemeService.Get("Border");
    public Color TextColor { get; set; } = FitnessTracker.Services.ThemeService.Get("TextPrimary");
}