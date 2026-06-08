using CommunityToolkit.Maui.Views;

namespace FitnessTracker.Views;

public partial class NewPersonalRecordPopup : Popup
{
    public NewPersonalRecordPopup(string exercise, double? oldRecord, double newRecord, DateTime date)
    {
        InitializeComponent();

        ExerciseLabel.Text = exercise;
        OldRecordLabel.Text = oldRecord.HasValue ? $"{oldRecord.Value:0.##} kg" : "Brak";
        NewRecordLabel.Text = $"{newRecord:0.##} kg";
        DateLabel.Text = $"Data: {date:dd.MM.yyyy HH:mm}";
    }

    private void OnCloseClicked(object sender, EventArgs e)
    {
        Close();
    }
}