using System.Runtime.InteropServices;
using System.Windows;
using AiDesktopAssistant.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AiDesktopAssistant.App.Services;

public sealed class WindowsClipboardSelectedTextProvider(ILogger<WindowsClipboardSelectedTextProvider> logger) : ISelectedTextProvider
{
    private const byte VkControl = 0x11;
    private const byte VkC = 0x43;
    private const uint KeyEventKeyUp = 0x0002;

    public async Task<string?> GetSelectedTextAsync(CancellationToken cancellationToken = default)
    {
        IDataObject? previousClipboard = null;

        try
        {
            previousClipboard = Clipboard.GetDataObject();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Unable to snapshot clipboard before selected text capture.");
        }

        try
        {
            SendCopyShortcut();
            await Task.Delay(150, cancellationToken);

            var selectedText = Clipboard.ContainsText() ? Clipboard.GetText() : null;
            return string.IsNullOrWhiteSpace(selectedText) ? null : selectedText.Trim();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to capture selected text from the active Windows application.");
            return null;
        }
        finally
        {
            if (previousClipboard is not null)
            {
                try
                {
                    Clipboard.SetDataObject(previousClipboard, copy: true);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Unable to restore clipboard after selected text capture.");
                }
            }
        }
    }

    private static void SendCopyShortcut()
    {
        keybd_event(VkControl, 0, 0, UIntPtr.Zero);
        keybd_event(VkC, 0, 0, UIntPtr.Zero);
        keybd_event(VkC, 0, KeyEventKeyUp, UIntPtr.Zero);
        keybd_event(VkControl, 0, KeyEventKeyUp, UIntPtr.Zero);
    }

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}
