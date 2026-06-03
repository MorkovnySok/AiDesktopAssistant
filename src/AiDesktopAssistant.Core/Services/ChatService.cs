using System.Runtime.CompilerServices;
using AiDesktopAssistant.Core.Ai;
using AiDesktopAssistant.Core.Entities;
using AiDesktopAssistant.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace AiDesktopAssistant.Core.Services;

public sealed class ChatService(
    IConversationRepository conversationRepository,
    IAiChatClient aiChatClient,
    IOptions<AiOptions> aiOptions) : IChatService
{
    public Task<Conversation> CreateConversationAsync(CancellationToken cancellationToken = default) =>
        conversationRepository.CreateAsync(cancellationToken);

    public async IAsyncEnumerable<AiStreamEvent> SendMessageAsync(
        Guid conversationId,
        string userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var conversation = await conversationRepository.GetByIdAsync(conversationId, cancellationToken)
            ?? throw new InvalidOperationException($"Conversation '{conversationId}' was not found.");

        var now = DateTime.UtcNow;
        if (conversation.Messages.Count == 0)
        {
            conversation.Title = CreateTitle(userMessage);
        }

        conversation.Messages.Add(new ChatMessage
        {
            ConversationId = conversation.Id,
            Role = MessageRole.User,
            Content = userMessage,
            CreatedAt = now
        });

        var assistantMessage = new ChatMessage
        {
            ConversationId = conversation.Id,
            Role = MessageRole.Assistant,
            Content = string.Empty,
            ReasoningContent = string.Empty,
            CreatedAt = DateTime.UtcNow
        };
        conversation.Messages.Add(assistantMessage);
        conversation.UpdatedAt = DateTime.UtcNow;

        var options = aiOptions.Value;
        var request = new AiChatRequest(
            options.Model,
            BuildRequestMessages(conversation),
            options.Think,
            options.SystemPrompt);

        var completed = false;

        try
        {
            await foreach (var streamEvent in aiChatClient.StreamAsync(request, cancellationToken).WithCancellation(cancellationToken))
            {
                switch (streamEvent.Type)
                {
                    case AiStreamEventType.Reasoning:
                        assistantMessage.ReasoningContent += streamEvent.Text;
                        break;
                    case AiStreamEventType.Content:
                        assistantMessage.Content += streamEvent.Text;
                        break;
                    case AiStreamEventType.Completed:
                        completed = true;
                        break;
                }

                yield return streamEvent;
            }
        }
        finally
        {
            conversation.UpdatedAt = DateTime.UtcNow;
            await conversationRepository.SaveAsync(conversation, CancellationToken.None);
        }

        if (!completed)
        {
            yield return new AiStreamEvent(AiStreamEventType.Completed);
        }
    }

    private static IReadOnlyList<AiChatMessage> BuildRequestMessages(Conversation conversation) =>
        conversation.Messages
            .Where(message => !string.IsNullOrWhiteSpace(message.Content))
            .OrderBy(message => message.CreatedAt)
            .Select(message => new AiChatMessage(message.Role, message.Content))
            .ToList();

    private static string CreateTitle(string userMessage)
    {
        var trimmed = userMessage.Trim();
        if (trimmed.Length == 0)
        {
            return "New conversation";
        }

        return trimmed.Length <= 48 ? trimmed : $"{trimmed[..48]}...";
    }
}
