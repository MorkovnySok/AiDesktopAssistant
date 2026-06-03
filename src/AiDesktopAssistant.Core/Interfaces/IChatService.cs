using AiDesktopAssistant.Core.Ai;
using AiDesktopAssistant.Core.Entities;

namespace AiDesktopAssistant.Core.Interfaces;

public interface IChatService
{
    Task<Conversation> CreateConversationAsync(CancellationToken cancellationToken = default);
    Task DeleteConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);

    IAsyncEnumerable<AiStreamEvent> SendMessageAsync(
        Guid conversationId,
        string userMessage,
        CancellationToken cancellationToken = default);
}
