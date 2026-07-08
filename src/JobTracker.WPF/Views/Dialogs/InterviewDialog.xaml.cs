using System.Globalization;
using System.Windows;
using JobTracker.Domain.Enums;

namespace JobTracker.WPF.Views.Dialogs;

/// <summary>
/// Simple modal for setting or modifying an interview's date, time and type.
/// Returns <see cref="ScheduledAt"/> and <see cref="SelectedType"/> when DialogResult is true.
/// </summary>
public partial class InterviewDialog : Window
{
    public DateTime ScheduledAt { get; private set; }
    public InterviewType SelectedType { get; private set; }

    /// <param name="header">Dialog title, e.g. "Set first interview" or "Modify interview date".</param>
    /// <param name="subHeader">Context line, e.g. the role and company.</param>
    /// <param name="initial">Existing date/time when modifying; null seeds tomorrow 10:00.</param>
    /// <param name="initialType">Existing type when modifying.</param>
    public InterviewDialog(string header, string subHeader,
        DateTime? initial = null, InterviewType initialType = InterviewType.Video)
    {
        InitializeComponent();

        HeaderText.Text = header;
        SubHeaderText.Text = subHeader;

        var seed = initial ?? DateTime.Today.AddDays(1).AddHours(10);
        DatePick.SelectedDate = seed.Date;

        // Hour/minute dropdowns make invalid times (letters, symbols, out-of-range) impossible.
        HourCombo.ItemsSource = Enumerable.Range(0, 24).Select(h => h.ToString("00")).ToList();
        MinuteCombo.ItemsSource = Enumerable.Range(0, 60).Select(m => m.ToString("00")).ToList();
        HourCombo.SelectedItem = seed.Hour.ToString("00");
        MinuteCombo.SelectedItem = seed.Minute.ToString("00");

        TypeCombo.ItemsSource = Enum.GetValues<InterviewType>();
        TypeCombo.SelectedItem = initialType;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DatePick.SelectedDate is not { } date)
        {
            ShowError("Please pick a date from the calendar.");
            return;
        }

        if (TypeCombo.SelectedItem is not InterviewType type)
        {
            ShowError("Please choose an interview type.");
            return;
        }

        // Both come from dropdowns, so parsing always succeeds and stays in range.
        var hour = int.Parse((string)HourCombo.SelectedItem, CultureInfo.InvariantCulture);
        var minute = int.Parse((string)MinuteCombo.SelectedItem, CultureInfo.InvariantCulture);

        ScheduledAt = date.Date.AddHours(hour).AddMinutes(minute);
        SelectedType = type;
        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
