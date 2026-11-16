using System.Windows.Input;

namespace FitnessTracker.Controls;

public partial class ActionTile : ContentView
{
    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        nameof(Icon), typeof(string), typeof(ActionTile), string.Empty,
        propertyChanged: (b, _, n) => ((ActionTile)b).IconLabel.Text = (string?)n);

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(ActionTile), string.Empty,
        propertyChanged: (b, _, n) => ((ActionTile)b).TitleLabel.Text = (string?)n);

    public static readonly BindableProperty SubtitleProperty = BindableProperty.Create(
        nameof(Subtitle), typeof(string), typeof(ActionTile), string.Empty,
        propertyChanged: (b, _, n) => ((ActionTile)b).SubtitleLabel.Text = (string?)n);

    /// <summary>Rodzina koloru z motywu: Blue, Green, Teal, Orange, Indigo, Red… (tło = &lt;Accent&gt;Soft).</summary>
    public static readonly BindableProperty AccentProperty = BindableProperty.Create(
        nameof(Accent), typeof(string), typeof(ActionTile), "Blue",
        propertyChanged: (b, _, n) => ((ActionTile)b).ApplyAccent((string)n));

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(ActionTile));

    public ActionTile()
    {
        InitializeComponent();
        ApplyAccent(Accent);
    }

    public string Icon { get => (string)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public string Accent { get => (string)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    private void ApplyAccent(string accent)
    {
        IconBox.SetDynamicResource(BackgroundColorProperty, accent + "Soft");
        IconLabel.SetDynamicResource(Label.TextColorProperty, accent);
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (Command?.CanExecute(null) == true)
            Command.Execute(null);
    }
}
