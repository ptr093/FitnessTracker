using Microsoft.Maui.Graphics;

namespace FitnessTracker.Models;

public class RecordItem
{
    public string Icon { get; set; } = "🏆";
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ValueText { get; set; } = string.Empty;
    public string DetailText { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
    public string ExtraText { get; set; } = "-";

    public Color AccentBackground { get; set; } = Color.FromArgb("#FEF3C7");
    public Color BadgeBackground { get; set; } = Color.FromArgb("#FFF7ED");
    public Color BadgeTextColor { get; set; } = Color.FromArgb("#C2410C");
}