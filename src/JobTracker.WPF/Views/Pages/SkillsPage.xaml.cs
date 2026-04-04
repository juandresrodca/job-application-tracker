using System.Windows.Controls;
using JobTracker.WPF.ViewModels;

namespace JobTracker.WPF.Views.Pages;

public partial class SkillsPage : Page
{
    private readonly SkillsViewModel _vm;

    public SkillsPage(SkillsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
        Loaded += async (_, _) => await _vm.RefreshAsync();
    }
}
