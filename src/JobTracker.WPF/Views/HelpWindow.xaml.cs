using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace JobTracker.WPF;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        ProcessStartInfo psi = new()
        {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true
        };
        Process.Start(psi);
        e.Handled = true;
    }
}
