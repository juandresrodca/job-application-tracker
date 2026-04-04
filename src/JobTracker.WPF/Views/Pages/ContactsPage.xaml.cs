using System.Windows.Controls;
using JobTracker.WPF.ViewModels;

namespace JobTracker.WPF.Views.Pages;

public partial class ContactsPage : Page
{
    private readonly ContactsViewModel _vm;

    public ContactsPage(ContactsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
        Loaded += async (_, _) => await _vm.RefreshAsync();
    }
}
