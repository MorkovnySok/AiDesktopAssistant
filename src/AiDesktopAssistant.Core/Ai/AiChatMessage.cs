using AiDesktopAssistant.Core.Entities;

namespace AiDesktopAssistant.Core.Ai;

public sealed record AiChatMessage(MessageRole Role, string Content);
