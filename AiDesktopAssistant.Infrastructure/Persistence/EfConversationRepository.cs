using AiDesktopAssistant.Core.Domain;
using AiDesktopAssistant.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AiDesktopAssistant.Infrastructure.Persistence;

public class EfConversationRepository(AssistantDbContext context) : IConversationRepository
{
    public async Task<Conversation> CreateAsync(CancellationToken cancellationToken = default)
    {
        var conversation = new Conversation();
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync(cancellationToken);
        return conversation;
    }

    public async Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Conversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Conversation>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await context.Conversations
            .AsNoTracking()
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        var tracked = await context.Conversations.AnyAsync(c => c.Id == conversation.Id, cancellationToken);
        if (!tracked)
        {
            context.Conversations.Add(conversation);
        }
        else
        {
            context.Conversations.Update(conversation);
        }
        await context.SaveChangesAsync(cancellationToken);
    }
}