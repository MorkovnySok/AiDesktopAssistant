using AiDesktopAssistant.Core.Entities;
using AiDesktopAssistant.Core.Interfaces;
using AiDesktopAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiDesktopAssistant.Infrastructure.Repositories;

public sealed class EfConversationRepository(AssistantDbContext dbContext) : IConversationRepository
{
    public async Task<Conversation> CreateAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Title = "New conversation",
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return conversation;
    }

    public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Conversations
            .Include(conversation => conversation.Messages)
            .FirstOrDefaultAsync(conversation => conversation.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Conversation>> ListAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Conversations
            .Include(conversation => conversation.Messages)
            .OrderByDescending(conversation => conversation.UpdatedAt)
            .ToListAsync(cancellationToken);

    public async Task SaveAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        dbContext.Update(conversation);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
