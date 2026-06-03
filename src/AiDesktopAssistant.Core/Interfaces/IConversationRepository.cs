using AiDesktopAssistant.Core.Entities;

namespace AiDesktopAssistant.Core.Interfaces;

public interface IConversationRepository
{
    Task<Conversation> CreateAsync(CancellationToken cancellationToken = default);
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Conversation>> ListAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddMessagesAsync(
        Guid conversationId,
        IReadOnlyList<ChatMessage> messages,
        string title,
        DateTime updatedAt,
        CancellationToken cancellationToken = default);

    Task UpdateAssistantMessageAsync(
        Guid conversationId,
        ChatMessage assistantMessage,
        DateTime updatedAt,
        CancellationToken cancellationToken = default);
}
