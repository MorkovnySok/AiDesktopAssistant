using AiDesktopAssistant.Core.Domain;
using AiDesktopAssistant.Core.Models;

namespace AiDesktopAssistant.Core.Interfaces;

public interface IChatService
{
    Task<Conversation> CreateConversationAsync(CancellationToken cancellationToken = default);

    IAsyncEnumerable<AiStreamEvent> SendMessageAsync(Guid conversationId,
        string userMessage,
        CancellationToken cancellationToken = default);
}