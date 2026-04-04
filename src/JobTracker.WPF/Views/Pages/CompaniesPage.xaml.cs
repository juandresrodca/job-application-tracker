using System.Windows.Controls;
using JobTracker.WPF.ViewModels;

namespace JobTracker.WPF.Views.Pages;

public partial class CompaniesPage : Page
{
    private readonly CompaniesViewModel _vm;

    public CompaniesPage(CompaniesViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
        Loaded += async (_, _) => await _vm.RefreshAsync();
    }
}
