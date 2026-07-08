using System.Windows;
using System.Windows.Controls;
using JobTracker.Application.DTOs;
using JobTracker.WPF.ViewModels;

namespace JobTracker.WPF.Views.Pages;

public partial class ApplicationFormPage : Page
{
    private readonly ApplicationFormViewModel _vm;

    public ApplicationFormPage(ApplicationFormViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = viewModel;

        viewModel.SaveCompleted += () =>
        {
            if (Window.GetWindow(this) is MainWindow main)
                main.NavigateCommand.Execute("Dashboard");
        };

        viewModel.Cancelled += () =>
        {
            if (Window.GetWindow(this) is MainWindow main)
                main.NavigateCommand.Execute("Dashboard");
        };
    }

    private async void DeleteInterview_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is InterviewDto interview)
            await _vm.DeleteInterviewAsync(interview);
    }

    private async void InterviewCompleted_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is InterviewDto interview)
            await _vm.ToggleInterviewCompletedAsync(interview);
    }
}
