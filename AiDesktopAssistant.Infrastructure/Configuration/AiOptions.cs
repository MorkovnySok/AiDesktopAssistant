namespace AiDesktopAssistant.Infrastructure.Configuration;

public class AiOptions
{
    public string Provider { get; set; } = "Ollama";
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "qwen2.5-coder:7b";
    public bool Think { get; set; } = false;
    public string? SystemPrompt { get; set; }
}