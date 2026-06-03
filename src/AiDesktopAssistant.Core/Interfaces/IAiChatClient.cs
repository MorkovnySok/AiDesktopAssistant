using AiDesktopAssistant.Core.Ai;

namespace AiDesktopAssistant.Core.Interfaces;

public interface IAiChatClient
{
    IAsyncEnumerable<AiStreamEvent> StreamAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default);
}
