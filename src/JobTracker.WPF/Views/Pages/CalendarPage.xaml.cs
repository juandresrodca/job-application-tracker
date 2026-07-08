using System.Windows.Controls;
using JobTracker.WPF.ViewModels;

namespace JobTracker.WPF.Views.Pages;

public partial class CalendarPage : Page
{
    private readonly CalendarViewModel _vm;

    public CalendarPage(CalendarViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
        Loaded += async (_, _) => await _vm.LoadAsync();
    }
}
