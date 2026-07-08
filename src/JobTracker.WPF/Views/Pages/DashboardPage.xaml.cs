using System.Windows;
using System.Windows.Controls;
using JobTracker.Application.DTOs;
using JobTracker.WPF.ViewModels;
using JobTracker.WPF.Views.Dialogs;

namespace JobTracker.WPF.Views.Pages;

public partial class DashboardPage : Page
{
    private readonly DashboardViewModel _vm;

    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = _vm;

        _vm.EditApplicationRequested += id =>
        {
            if (Window.GetWindow(this) is MainWindow main)
                main.NavigateToEdit(id);
        };

        _vm.NewApplicationRequested += () =>
        {
            if (Window.GetWindow(this) is MainWindow main)
                main.NavigateCommand.Execute("NewApplication");
        };

        Loaded += async (_, _) => await _vm.LoadDataAsync();
    }

    // ── Right-click interview scheduling ─────────────────────────────────────
    // The clicked menu item carries the row's DTO as its DataContext, so the action
    // always targets the row that was right-clicked (not the selected row).

    private static JobApplicationDto? RowOf(object sender)
        => (sender as FrameworkElement)?.DataContext as JobApplicationDto;

    /// <summary>"Set first interview" and "Set new interview" both create a fresh interview.</summary>
    private async void SetInterview_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } app) return;

        var header = app.HasInterview ? "Set new interview" : "Set first interview";
        var dlg = new InterviewDialog(header, $"{app.RoleName} · {app.CompanyName}")
        {
            Owner = Window.GetWindow(this)
        };

        if (dlg.ShowDialog() == true)
            await _vm.CreateInterviewAsync(app.Id, dlg.ScheduledAt, dlg.SelectedType);
    }

    /// <summary>Reschedules the row's next (or most recent) interview.</summary>
    private async void ModifyInterview_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } app || app.NextInterviewId is not { } interviewId) return;

        var dlg = new InterviewDialog(
            "Modify interview date", $"{app.RoleName} · {app.CompanyName}",
            initial: app.NextInterviewAt)
        {
            Owner = Window.GetWindow(this)
        };

        if (dlg.ShowDialog() == true)
            await _vm.UpdateInterviewDateAsync(interviewId, dlg.ScheduledAt, dlg.SelectedType);
    }
}
