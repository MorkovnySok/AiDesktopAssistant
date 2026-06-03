namespace AiDesktopAssistant.Core.Ai;

public sealed record AiStreamEvent(AiStreamEventType Type, string? Text = null);
