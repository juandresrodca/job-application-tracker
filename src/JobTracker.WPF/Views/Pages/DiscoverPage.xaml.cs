using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using JobTracker.WPF.ViewModels;

namespace JobTracker.WPF.Views.Pages;

public partial class DiscoverPage : Page
{
    private readonly DiscoverViewModel _vm;

    public DiscoverPage(DiscoverViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;

        _vm.TrackRequested += job =>
        {
            if (Window.GetWindow(this) is MainWindow main)
                main.NavigateToNewFromDiscovery(job);
        };
    }

    private void OpenJob_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not DiscoveredJobVm job) return;

        // Open the posting in the default browser
        Process.Start(new ProcessStartInfo(job.Url) { UseShellExecute = true });
    }

    private void TrackJob_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not DiscoveredJobVm job) return;
        _vm.RequestTrack(job);
    }
}
