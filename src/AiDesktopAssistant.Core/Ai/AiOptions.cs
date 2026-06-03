namespace AiDesktopAssistant.Core.Ai;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public string Provider { get; set; } = "Ollama";
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "qwen2.5-coder:7b";
    public bool Think { get; set; }
    public string? SystemPrompt { get; set; } = "You are a helpful coding assistant. Answer clearly and practically.";
}
