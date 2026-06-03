using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Threading;
using AiDesktopAssistant.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using Forms = System.Windows.Forms;
using IDataObject = System.Windows.IDataObject;
using TextDataFormat = System.Windows.TextDataFormat;

namespace AiDesktopAssistant.App.Services;

public sealed class ClipboardSelectedTextCaptureService(
    ILogger<ClipboardSelectedTextCaptureService> logger) : ISelectedTextCaptureService
{
    private const ushort VirtualKeyControl = 0x11;
    private const ushort VirtualKeyShift = 0x10;
    private const ushort VirtualKeyMenu = 0x12;
    private const ushort VirtualKeyC = 0x43;
    private const ushort VirtualKeyInsert = 0x2D;
    private const ushort VirtualKeySpace = 0x20;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const int WindowTitleCapacity = 256;

    public async Task<string?> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            return await CaptureOnDispatcherAsync(cancellationToken);
        }

        var operation = dispatcher.InvokeAsync(
            () => CaptureOnDispatcherAsync(cancellationToken),
            DispatcherPriority.Send,
            cancellationToken);

        return await await operation.Task;
    }

    private async Task<string?> CaptureOnDispatcherAsync(CancellationToken cancellationToken)
    {
        IDataObject? previousClipboard = null;
        var hadPreviousClipboard = false;

        try
        {
            logger.LogDebug("Selected text capture started.");
            var targetWindow = GetForegroundWindow();
            logger.LogDebug(
                "Foreground window before capture: {WindowHandle}, Title: {WindowTitle}",
                targetWindow,
                GetWindowTitle(targetWindow));

            var automationText = TryCaptureWithUiAutomation(targetWindow);
            if (!string.IsNullOrWhiteSpace(automationText))
            {
                logger.LogInformation("Selected text captured with UI Automation. Length: {Length}", automationText.Length);
                return automationText;
            }

            logger.LogInformation("UI Automation did not return selected text. Falling back to clipboard copy.");

            previousClipboard = await TryGetClipboardDataAsync(cancellationToken);
            hadPreviousClipboard = previousClipboard is not null;
            logger.LogDebug("Previous clipboard captured: {HasPreviousClipboard}", hadPreviousClipboard);

            await WaitForHotkeyReleaseAsync(cancellationToken);
            var copiedText = await TryClipboardCopyStrategiesAsync(targetWindow, cancellationToken);
            logger.LogDebug(
                "Selected text capture finished. HasText: {HasText}, Length: {Length}",
                !string.IsNullOrWhiteSpace(copiedText),
                copiedText?.Length ?? 0);

            return copiedText;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ExternalException or Win32Exception or InvalidOperationException)
        {
            logger.LogWarning(ex, "Failed to capture selected text from the active application.");
            return null;
        }
        finally
        {
            await RestoreClipboardAsync(previousClipboard, hadPreviousClipboard, cancellationToken);
        }
    }

    private string? TryCaptureWithUiAutomation(nint targetWindow)
    {
        try
        {
            var focusedElement = AutomationElement.FocusedElement;
            LogAutomationElement("Focused UI Automation element", focusedElement);

            var selectedText = TryGetSelectedText(focusedElement);
            if (!string.IsNullOrWhiteSpace(selectedText))
            {
                return selectedText;
            }

            if (targetWindow == nint.Zero)
            {
                return null;
            }

            var windowElement = AutomationElement.FromHandle(targetWindow);
            LogAutomationElement("Foreground UI Automation window", windowElement);
            return TryGetSelectedText(windowElement);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ElementNotAvailableException or COMException)
        {
            logger.LogDebug(ex, "UI Automation selected text capture failed.");
            return null;
        }
    }

    private string? TryGetSelectedText(AutomationElement? element)
    {
        if (element is null)
        {
            return null;
        }

        if (!element.TryGetCurrentPattern(TextPattern.Pattern, out var pattern)
            || pattern is not TextPattern textPattern)
        {
            logger.LogDebug("UI Automation element does not support TextPattern.");
            return null;
        }

        var ranges = textPattern.GetSelection();
        if (ranges.Length == 0)
        {
            logger.LogDebug("UI Automation TextPattern returned no selection ranges.");
            return null;
        }

        var selectedText = new StringBuilder();
        foreach (var range in ranges)
        {
            selectedText.Append(range.GetText(-1));
        }

        return selectedText.Length == 0 ? null : selectedText.ToString();
    }

    private void LogAutomationElement(string label, AutomationElement? element)
    {
        if (element is null)
        {
            logger.LogDebug("{Label}: <none>", label);
            return;
        }

        try
        {
            logger.LogDebug(
                "{Label}: Name='{Name}', Class='{ClassName}', ControlType='{ControlType}', ProcessId={ProcessId}",
                label,
                element.Current.Name,
                element.Current.ClassName,
                element.Current.ControlType.ProgrammaticName,
                element.Current.ProcessId);
        }
        catch (ElementNotAvailableException)
        {
            logger.LogDebug("{Label}: element is no longer available.", label);
        }
    }

    private async Task<string?> TryClipboardCopyStrategiesAsync(nint targetWindow, CancellationToken cancellationToken)
    {
        var strategies = new (string Name, Action Send)[]
        {
            ("SendKeys Ctrl+C", () => Forms.SendKeys.SendWait("^c")),
            ("SendInput Ctrl+C", SendCopyShortcut),
            ("SendKeys Ctrl+Insert", () => Forms.SendKeys.SendWait("^{INSERT}")),
            ("SendInput Ctrl+Insert", SendCopyWithCtrlInsert),
            ("SendKeys Ctrl+Shift+C", () => Forms.SendKeys.SendWait("^+c")),
            ("SendInput Ctrl+Shift+C", SendCopyWithCtrlShiftC)
        };

        foreach (var (name, send) in strategies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Clipboard.Clear();
            logger.LogDebug("Clipboard cleared before copy strategy: {Strategy}", name);
            RestoreForegroundWindow(targetWindow);
            await Task.Delay(75, cancellationToken);

            logger.LogDebug("Sending copy strategy: {Strategy}", name);
            send();

            var copiedText = await WaitForCopiedTextAsync(cancellationToken);
            logger.LogDebug(
                "Copy strategy completed: {Strategy}. HasText: {HasText}, Length: {Length}",
                name,
                !string.IsNullOrWhiteSpace(copiedText),
                copiedText?.Length ?? 0);

            if (!string.IsNullOrWhiteSpace(copiedText))
            {
                logger.LogInformation("Selected text captured with clipboard strategy: {Strategy}. Length: {Length}", name, copiedText.Length);
                return copiedText;
            }
        }

        return null;
    }

    private static async Task<string?> WaitForCopiedTextAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Clipboard.ContainsText(TextDataFormat.UnicodeText))
            {
                var text = Clipboard.GetText(TextDataFormat.UnicodeText);
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }

            await Task.Delay(50, cancellationToken);
        }

        return null;
    }

    private async Task WaitForHotkeyReleaseAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsKeyDown(VirtualKeyControl)
                && !IsKeyDown(VirtualKeyMenu)
                && !IsKeyDown(VirtualKeySpace))
            {
                logger.LogDebug("Hotkey keys released before sending copy shortcut.");
                return;
            }

            await Task.Delay(25, cancellationToken);
        }

        logger.LogDebug("Timed out waiting for hotkey keys to be released. Sending copy shortcut anyway.");
    }

    private void RestoreForegroundWindow(nint targetWindow)
    {
        if (targetWindow == nint.Zero)
        {
            logger.LogDebug("No foreground window handle was available before sending copy shortcut.");
            return;
        }

        var currentWindow = GetForegroundWindow();
        logger.LogDebug(
            "Foreground window before copy shortcut: {WindowHandle}, Title: {WindowTitle}",
            currentWindow,
            GetWindowTitle(currentWindow));

        if (currentWindow == targetWindow)
        {
            return;
        }

        var restored = SetForegroundWindow(targetWindow);
        logger.LogDebug(
            "Restored foreground window before copy shortcut: {Restored}. Target: {WindowHandle}, Title: {WindowTitle}",
            restored,
            targetWindow,
            GetWindowTitle(targetWindow));
    }

    private static bool IsKeyDown(ushort virtualKey) =>
        (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static string GetWindowTitle(nint windowHandle)
    {
        if (windowHandle == nint.Zero)
        {
            return "<none>";
        }

        var title = new StringBuilder(WindowTitleCapacity);
        var length = GetWindowText(windowHandle, title, title.Capacity);
        return length <= 0 ? "<untitled>" : title.ToString();
    }

    private static async Task<IDataObject?> TryGetClipboardDataAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                return Clipboard.GetDataObject();
            }
            catch (ExternalException) when (attempt < 2)
            {
                await Task.Delay(25, cancellationToken);
            }
        }

        return null;
    }

    private async Task RestoreClipboardAsync(
        IDataObject? previousClipboard,
        bool hadPreviousClipboard,
        CancellationToken cancellationToken)
    {
        try
        {
            if (hadPreviousClipboard && previousClipboard is not null)
            {
                Clipboard.SetDataObject(previousClipboard, true);
                logger.LogDebug("Previous clipboard restored.");
            }
            else
            {
                Clipboard.Clear();
                logger.LogDebug("Clipboard cleared because there was no previous clipboard data.");
            }
        }
        catch (ExternalException)
        {
            logger.LogWarning("Clipboard restore failed on first attempt. Retrying once.");
            await Task.Delay(25, cancellationToken);

            if (hadPreviousClipboard && previousClipboard is not null)
            {
                Clipboard.SetDataObject(previousClipboard, true);
                logger.LogDebug("Previous clipboard restored on retry.");
            }
        }
    }

    private static void SendCopyShortcut()
    {
        var inputs = new[]
        {
            CreateKeyboardInput(VirtualKeyMenu, keyUp: true),
            CreateKeyboardInput(VirtualKeyControl, keyUp: true),
            CreateKeyboardInput(VirtualKeyControl, keyUp: false),
            CreateKeyboardInput(VirtualKeyC, keyUp: false),
            CreateKeyboardInput(VirtualKeyC, keyUp: true),
            CreateKeyboardInput(VirtualKeyControl, keyUp: true)
        };

        SendKeyboardInputs(inputs);
    }

    private static void SendCopyWithCtrlInsert()
    {
        var inputs = new[]
        {
            CreateKeyboardInput(VirtualKeyMenu, keyUp: true),
            CreateKeyboardInput(VirtualKeyControl, keyUp: true),
            CreateKeyboardInput(VirtualKeyControl, keyUp: false),
            CreateKeyboardInput(VirtualKeyInsert, keyUp: false),
            CreateKeyboardInput(VirtualKeyInsert, keyUp: true),
            CreateKeyboardInput(VirtualKeyControl, keyUp: true)
        };

        SendKeyboardInputs(inputs);
    }

    private static void SendCopyWithCtrlShiftC()
    {
        var inputs = new[]
        {
            CreateKeyboardInput(VirtualKeyMenu, keyUp: true),
            CreateKeyboardInput(VirtualKeyControl, keyUp: true),
            CreateKeyboardInput(VirtualKeyShift, keyUp: true),
            CreateKeyboardInput(VirtualKeyControl, keyUp: false),
            CreateKeyboardInput(VirtualKeyShift, keyUp: false),
            CreateKeyboardInput(VirtualKeyC, keyUp: false),
            CreateKeyboardInput(VirtualKeyC, keyUp: true),
            CreateKeyboardInput(VirtualKeyShift, keyUp: true),
            CreateKeyboardInput(VirtualKeyControl, keyUp: true)
        };

        SendKeyboardInputs(inputs);
    }

    private static void SendKeyboardInputs(Input[] inputs)
    {
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private static Input CreateKeyboardInput(ushort virtualKey, bool keyUp) =>
        new()
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = virtualKey,
                    Flags = keyUp ? KeyEventKeyUp : 0
                }
            }
        };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(ushort virtualKey);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(nint hWnd, StringBuilder text, int maxCount);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;

        [FieldOffset(0)]
        public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }
}
