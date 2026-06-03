namespace AiDesktopAssistant.Core.Interfaces;

public interface ISelectedTextCaptureService
{
    Task<string?> CaptureAsync(CancellationToken cancellationToken = default);
}
