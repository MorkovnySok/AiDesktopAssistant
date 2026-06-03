namespace AiDesktopAssistant.Core.Interfaces;

public interface IGlobalHotkeyService : IAsyncDisposable
{
    event EventHandler? Pressed;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
