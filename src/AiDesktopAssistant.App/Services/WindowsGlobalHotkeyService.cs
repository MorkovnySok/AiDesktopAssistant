using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using AiDesktopAssistant.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;

namespace AiDesktopAssistant.App.Services;

public sealed class WindowsGlobalHotkeyService(
    ILogger<WindowsGlobalHotkeyService> logger) : IGlobalHotkeyService
{
    private const int HotkeyId = 0x4149;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;

    private HwndSource? _source;
    private nint _windowHandle;
    private bool _isStarted;

    public event EventHandler? Pressed;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await Application.Current.Dispatcher.InvokeAsync(StartCore, DispatcherPriority.Normal, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await Application.Current.Dispatcher.InvokeAsync(StopCore, DispatcherPriority.Normal, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to stop global hotkey service during disposal.");
        }
    }

    private void StartCore()
    {
        if (_isStarted)
        {
            return;
        }

        var window = Application.Current.MainWindow;
        if (window is null)
        {
            logger.LogWarning("Global hotkey was not registered because the main window is not available.");
            return;
        }

        var helper = new WindowInteropHelper(window);
        _windowHandle = helper.Handle == nint.Zero ? helper.EnsureHandle() : helper.Handle;

        _source = HwndSource.FromHwnd(_windowHandle);
        if (_source is null)
        {
            logger.LogWarning("Global hotkey was not registered because the window message source is not available.");
            return;
        }

        _source.AddHook(WndProc);

        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(Key.Space);
        if (!RegisterHotKey(_windowHandle, HotkeyId, ModControl | ModAlt | ModNoRepeat, virtualKey))
        {
            var errorCode = Marshal.GetLastWin32Error();
            _source.RemoveHook(WndProc);
            _source = null;
            logger.LogWarning("Global hotkey Ctrl+Alt+Space was not registered. Win32 error: {ErrorCode}", errorCode);
            return;
        }

        _isStarted = true;
        logger.LogInformation("Global hotkey Ctrl+Alt+Space registered.");
    }

    private void StopCore()
    {
        if (!_isStarted)
        {
            return;
        }

        if (_windowHandle != nint.Zero)
        {
            UnregisterHotKey(_windowHandle, HotkeyId);
        }

        _source?.RemoveHook(WndProc);
        _source = null;
        _windowHandle = nint.Zero;
        _isStarted = false;
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            logger.LogDebug("Global hotkey Ctrl+Alt+Space pressed.");
            Pressed?.Invoke(this, EventArgs.Empty);
        }

        return nint.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);
}
