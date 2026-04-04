using System.Windows;
using System.Windows.Controls;
using JobTracker.WPF.ViewModels;

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
}
