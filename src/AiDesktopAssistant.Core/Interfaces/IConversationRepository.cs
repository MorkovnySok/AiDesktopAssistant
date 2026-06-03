using AiDesktopAssistant.Core.Entities;

namespace AiDesktopAssistant.Core.Interfaces;

public interface IConversationRepository
{
    Task<Conversation> CreateAsync(CancellationToken cancellationToken = default);
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Conversation>> ListAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(Conversation conversation, CancellationToken cancellationToken = default);
}
