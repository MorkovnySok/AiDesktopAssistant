using System.Windows;
using Application = System.Windows.Application;

namespace AiDesktopAssistant.App.Services;

public sealed class MainWindowActivationService : IMainWindowActivationService
{
    public void Activate()
    {
        var window = Application.Current.MainWindow;
        if (window is null)
        {
            return;
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Show();
        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }
}
