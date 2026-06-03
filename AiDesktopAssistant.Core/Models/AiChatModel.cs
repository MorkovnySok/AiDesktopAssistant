using AiDesktopAssistant.Core.Domain;

namespace AiDesktopAssistant.Core.Models;

public record AiChatMessage(MessageRole Role, string Content);

public record AiChatRequest(
    string Model, 
    IReadOnlyList<AiChatMessage> Messages, 
    bool Think, 
    string? SystemPrompt);

public record AiStreamEvent(AiStreamEventType Type, string? Text);
