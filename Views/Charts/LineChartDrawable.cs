using Microsoft.Maui.Graphics;

namespace FitnessTracker.Views.Charts;

public record ChartPoint(DateTime Date, double Value);

/// <summary>
/// Prosty wykres liniowy rysowany przez GraphicsView (bez zewnętrznych bibliotek).
/// </summary>
public class LineChartDrawable : IDrawable
{
    public IReadOnlyList<ChartPoint> Points { get; set; } = Array.Empty<ChartPoint>();
    public string Unit { get; set; } = "kg";

    // Kolory czytane z motywu przy każdym rysowaniu – wykres przełącza się razem z motywem
    private static Color LineColor => FitnessTracker.Services.ThemeService.Get("Indigo");
    private static Color AreaColor => LineColor.WithAlpha(0.14f);
    private static Color GridColor => FitnessTracker.Services.ThemeService.Get("Border");
    private static Color TextColor => FitnessTracker.Services.ThemeService.Get("TextMuted");
    private static Color BestColor => FitnessTracker.Services.ThemeService.Get("Amber");

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        const float left = 44, right = 12, top = 14, bottom = 26;
        var plot = new RectF(dirtyRect.Left + left, dirtyRect.Top + top,
            dirtyRect.Width - left - right, dirtyRect.Height - top - bottom);

        canvas.FontSize = 10;
        canvas.FontColor = TextColor;

        if (Points.Count == 0 || plot.Width <= 0 || plot.Height <= 0)
        {
            canvas.DrawString("Brak danych do wykresu", dirtyRect, HorizontalAlignment.Center, VerticalAlignment.Center);
            return;
        }

        var min = Points.Min(p => p.Value);
        var max = Points.Max(p => p.Value);
        if (Math.Abs(max - min) < 0.001)
        {
            min -= 1;
            max += 1;
        }
        var pad = (max - min) * 0.1;
        min = Math.Max(0, min - pad);
        max += pad;

        // Siatka + oś Y
        canvas.StrokeColor = GridColor;
        canvas.StrokeSize = 1;
        const int gridLines = 4;
        for (var i = 0; i <= gridLines; i++)
        {
            var y = plot.Bottom - plot.Height * i / gridLines;
            canvas.DrawLine(plot.Left, y, plot.Right, y);
            var value = min + (max - min) * i / gridLines;
            canvas.DrawString($"{value:0}", 0, y - 7, left - 6, 14, HorizontalAlignment.Right, VerticalAlignment.Center);
        }

        var firstTicks = Points[0].Date.Ticks;
        var span = Math.Max(1, Points[^1].Date.Ticks - firstTicks);

        PointF Map(ChartPoint p)
        {
            var x = Points.Count == 1
                ? plot.Center.X
                : plot.Left + (float)((p.Date.Ticks - firstTicks) / (double)span) * plot.Width;
            var y = plot.Bottom - (float)((p.Value - min) / (max - min)) * plot.Height;
            return new PointF(x, y);
        }

        var mapped = Points.Select(Map).ToList();

        // Wypełnienie pod linią
        if (mapped.Count > 1)
        {
            var area = new PathF();
            area.MoveTo(mapped[0].X, plot.Bottom);
            foreach (var pt in mapped) area.LineTo(pt);
            area.LineTo(mapped[^1].X, plot.Bottom);
            area.Close();
            canvas.FillColor = AreaColor;
            canvas.FillPath(area);

            var line = new PathF();
            line.MoveTo(mapped[0]);
            foreach (var pt in mapped.Skip(1)) line.LineTo(pt);
            canvas.StrokeColor = LineColor;
            canvas.StrokeSize = 2.5f;
            canvas.StrokeLineJoin = LineJoin.Round;
            canvas.DrawPath(line);
        }

        // Punkty, najlepszy wynik wyróżniony
        var bestIndex = Points.Select((p, i) => (p.Value, i)).MaxBy(x => x.Value).i;
        for (var i = 0; i < mapped.Count; i++)
        {
            canvas.FillColor = i == bestIndex ? BestColor : LineColor;
            canvas.FillCircle(mapped[i], i == bestIndex ? 5 : 3.5f);
        }

        var best = Points[bestIndex];
        var bestPt = mapped[bestIndex];
        canvas.FontColor = FitnessTracker.Services.ThemeService.Get("AmberText");
        canvas.DrawString($"{best.Value:0.#} {Unit}", bestPt.X - 40, bestPt.Y - 20, 80, 14,
            HorizontalAlignment.Center, VerticalAlignment.Center);

        // Oś X: pierwsza i ostatnia data
        canvas.FontColor = TextColor;
        canvas.DrawString(Points[0].Date.ToString("dd.MM.yy"), plot.Left, plot.Bottom + 6, 60, 14,
            HorizontalAlignment.Left, VerticalAlignment.Center);
        if (Points.Count > 1)
            canvas.DrawString(Points[^1].Date.ToString("dd.MM.yy"), plot.Right - 60, plot.Bottom + 6, 60, 14,
                HorizontalAlignment.Right, VerticalAlignment.Center);
    }
}
