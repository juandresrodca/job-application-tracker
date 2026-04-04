using System.Windows;
using System.Windows.Controls;
using JobTracker.WPF.ViewModels;

namespace JobTracker.WPF.Views.Pages;

public partial class ApplicationFormPage : Page
{
    public ApplicationFormPage(ApplicationFormViewModel viewModel)
    {
        InitializeComponent();
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
}
