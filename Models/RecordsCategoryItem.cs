using Microsoft.Maui.Graphics;

namespace FitnessTracker.Models;

public class RecordsCategoryItem
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Icon { get; set; } = "🏆";

    public Color BackgroundColor { get; set; } = FitnessTracker.Services.ThemeService.Get("Surface");
    public Color BorderColor { get; set; } = FitnessTracker.Services.ThemeService.Get("Border");
    public Color TextColor { get; set; } = FitnessTracker.Services.ThemeService.Get("TextPrimary");
}