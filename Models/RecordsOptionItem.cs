using Microsoft.Maui.Graphics;

namespace FitnessTracker.Models;

public class RecordsOptionItem
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;

    public Color BackgroundColor { get; set; } = Color.FromArgb("#FFFFFF");
    public Color BorderColor { get; set; } = Color.FromArgb("#E2E8F0");
    public Color TextColor { get; set; } = Color.FromArgb("#1E293B");
}