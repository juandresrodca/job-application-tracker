using System.Windows;
using Microsoft.Win32;

namespace JobTracker.WPF.Services;

/// <summary>Abstracts modal dialogs so ViewModels remain testable.</summary>
public interface IDialogService
{
    bool Confirm(string title, string message);
    void Alert(string title, string message);
    string? SelectFile(string title, string filter);
}

public class WpfDialogService : IDialogService
{
    public bool Confirm(string title, string message)
    {
        var result = MessageBox.Show(
            message, title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        return result == MessageBoxResult.Yes;
    }

    public void Alert(string title, string message)
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public string? SelectFile(string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
            CheckPathExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
