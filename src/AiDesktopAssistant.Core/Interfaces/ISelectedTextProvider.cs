namespace AiDesktopAssistant.Core.Interfaces;

public interface ISelectedTextProvider
{
    Task<string?> GetSelectedTextAsync(CancellationToken cancellationToken = default);
}
