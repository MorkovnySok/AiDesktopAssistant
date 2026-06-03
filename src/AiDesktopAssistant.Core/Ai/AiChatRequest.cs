namespace AiDesktopAssistant.Core.Ai;

public sealed record AiChatRequest(
    string Model,
    IReadOnlyList<AiChatMessage> Messages,
    bool Think,
    string? SystemPrompt);
