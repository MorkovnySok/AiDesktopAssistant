namespace AiDesktopAssistant.Core.Domain;

public enum MessageRole
{
    System,
    User,
    Assistant
}

public enum AiStreamEventType
{
    Reasoning,
    Content,
    Completed
}
