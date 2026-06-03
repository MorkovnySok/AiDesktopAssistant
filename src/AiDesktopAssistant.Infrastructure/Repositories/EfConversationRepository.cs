using AiDesktopAssistant.Core.Entities;
using AiDesktopAssistant.Core.Interfaces;
using AiDesktopAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiDesktopAssistant.Infrastructure.Repositories;

public sealed class EfConversationRepository(IDbContextFactory<AssistantDbContext> dbContextFactory) : IConversationRepository
{
    public async Task<Conversation> CreateAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
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

    public async Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Conversations
            .AsNoTracking()
            .Include(conversation => conversation.Messages)
            .FirstOrDefaultAsync(conversation => conversation.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Conversation>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Conversations
            .AsNoTracking()
            .Include(conversation => conversation.Messages)
            .OrderByDescending(conversation => conversation.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var conversation = await dbContext.Conversations
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (conversation is null)
        {
            return;
        }

        dbContext.Conversations.Remove(conversation);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddMessagesAsync(
        Guid conversationId,
        IReadOnlyList<ChatMessage> messages,
        string title,
        DateTime updatedAt,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var conversation = await dbContext.Conversations
            .FirstOrDefaultAsync(item => item.Id == conversationId, cancellationToken)
            ?? throw new InvalidOperationException($"Conversation '{conversationId}' was not found.");

        conversation.Title = title;
        conversation.UpdatedAt = updatedAt;

        foreach (var message in messages)
        {
            message.ConversationId = conversationId;
            dbContext.Messages.Add(message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAssistantMessageAsync(
        Guid conversationId,
        ChatMessage assistantMessage,
        DateTime updatedAt,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var conversation = await dbContext.Conversations
            .FirstOrDefaultAsync(item => item.Id == conversationId, cancellationToken)
            ?? throw new InvalidOperationException($"Conversation '{conversationId}' was not found.");

        var message = await dbContext.Messages
            .FirstOrDefaultAsync(item => item.Id == assistantMessage.Id && item.ConversationId == conversationId, cancellationToken);

        if (message is null)
        {
            assistantMessage.ConversationId = conversationId;
            dbContext.Messages.Add(assistantMessage);
        }
        else
        {
            message.Content = assistantMessage.Content;
            message.ReasoningContent = assistantMessage.ReasoningContent;
            message.Role = assistantMessage.Role;
            message.CreatedAt = assistantMessage.CreatedAt;
        }

        conversation.UpdatedAt = updatedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
