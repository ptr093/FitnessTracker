namespace FitnessTracker.Controls;

public partial class PageHeader : ContentView
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(PageHeader), string.Empty,
        propertyChanged: (b, _, n) => ((PageHeader)b).TitleLabel.Text = (string?)n);

    public static readonly BindableProperty SubtitleProperty = BindableProperty.Create(
        nameof(Subtitle), typeof(string), typeof(PageHeader), string.Empty,
        propertyChanged: (b, _, n) =>
        {
            var header = (PageHeader)b;
            header.SubtitleLabel.Text = (string?)n;
            header.SubtitleLabel.IsVisible = !string.IsNullOrWhiteSpace((string?)n);
        });

    public static readonly BindableProperty ShowBackProperty = BindableProperty.Create(
        nameof(ShowBack), typeof(bool), typeof(PageHeader), true,
        propertyChanged: (b, _, n) => ((PageHeader)b).BackButton.IsVisible = (bool)n);

    /// <summary>Nazwa koloru z motywu, np. "Hero", "HeroRun", "TealStrong".</summary>
    public static readonly BindableProperty BackgroundKeyProperty = BindableProperty.Create(
        nameof(BackgroundKey), typeof(string), typeof(PageHeader), "Hero",
        propertyChanged: (b, _, n) => ((PageHeader)b).ApplyBackground((string)n));

    public static readonly BindableProperty TrailingContentProperty = BindableProperty.Create(
        nameof(TrailingContent), typeof(View), typeof(PageHeader),
        propertyChanged: (b, _, n) =>
        {
            var header = (PageHeader)b;
            header.TrailingHost.Content = (View?)n;
            header.TrailingHost.IsVisible = n != null;
        });

    public static readonly BindableProperty BodyContentProperty = BindableProperty.Create(
        nameof(BodyContent), typeof(View), typeof(PageHeader),
        propertyChanged: (b, _, n) =>
        {
            var header = (PageHeader)b;
            header.BodyHost.Content = (View?)n;
            header.BodyHost.IsVisible = n != null;
        });

    public PageHeader()
    {
        InitializeComponent();
        ApplyBackground(BackgroundKey);
        SubtitleLabel.IsVisible = false;
        TrailingHost.IsVisible = false;
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public bool ShowBack
    {
        get => (bool)GetValue(ShowBackProperty);
        set => SetValue(ShowBackProperty, value);
    }

    public string BackgroundKey
    {
        get => (string)GetValue(BackgroundKeyProperty);
        set => SetValue(BackgroundKeyProperty, value);
    }

    public View? TrailingContent
    {
        get => (View?)GetValue(TrailingContentProperty);
        set => SetValue(TrailingContentProperty, value);
    }

    public View? BodyContent
    {
        get => (View?)GetValue(BodyContentProperty);
        set => SetValue(BodyContentProperty, value);
    }

    private void ApplyBackground(string key) =>
        HeaderBorder.SetDynamicResource(BackgroundColorProperty, key);

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        if (Shell.Current?.Navigation.NavigationStack.Count > 1)
            await Shell.Current.GoToAsync("..");
    }
}
