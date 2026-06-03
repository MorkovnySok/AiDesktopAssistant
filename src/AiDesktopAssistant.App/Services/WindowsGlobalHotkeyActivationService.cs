using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;

namespace AiDesktopAssistant.App.Services;

public sealed class WindowsGlobalHotkeyActivationService(ILogger<WindowsGlobalHotkeyActivationService> logger) : IAssistantActivationService
{
    private const int HotkeyId = 0xA11A;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint VkSpace = 0x20;

    private HwndSource? _source;
    private IntPtr _handle;
    private Func<Task<string?>>? _activateAsync;
    private Window? _window;
    private bool _registered;

    public void Attach(MainWindow window, Func<Task<string?>> activateAsync)
    {
        _window = window;
        _activateAsync = activateAsync;
        window.SourceInitialized += OnSourceInitialized;
        window.Closed += OnClosed;
    }

    public void Dispose()
    {
        UnregisterHotkey();
        if (_window is not null)
        {
            _window.SourceInitialized -= OnSourceInitialized;
            _window.Closed -= OnClosed;
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (_window is null)
        {
            return;
        }

        _handle = new WindowInteropHelper(_window).Handle;
        _source = HwndSource.FromHwnd(_handle);
        _source?.AddHook(WndProc);

        _registered = RegisterHotKey(_handle, HotkeyId, ModControl | ModAlt, VkSpace);
        if (!_registered)
        {
            logger.LogWarning("Unable to register global hotkey Ctrl+Alt+Space. Win32 error: {ErrorCode}", Marshal.GetLastPInvokeError());
        }
    }

    private void OnClosed(object? sender, EventArgs e) => Dispose();

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            _ = ActivateAsync();
        }

        return IntPtr.Zero;
    }

    private async Task ActivateAsync()
    {
        if (_window is null || _activateAsync is null)
        {
            return;
        }

        var selectedText = await _activateAsync();

        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();
        _window.Topmost = true;
        _window.Topmost = false;

        logger.LogDebug("Assistant activated by global hotkey. Selected text captured: {HasSelectedText}", !string.IsNullOrWhiteSpace(selectedText));
    }

    private void UnregisterHotkey()
    {
        _source?.RemoveHook(WndProc);
        _source = null;

        if (_registered && _handle != IntPtr.Zero)
        {
            UnregisterHotKey(_handle, HotkeyId);
            _registered = false;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
