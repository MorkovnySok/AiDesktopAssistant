namespace AiDesktopAssistant.Core.Domain;

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public MessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ReasoningContent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}