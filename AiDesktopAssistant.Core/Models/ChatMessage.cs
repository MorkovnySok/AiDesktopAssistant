namespace AiDesktopAssistant.Models;

public class ChatMessage(string Role, string Content)
{
    public string Role { get; set; } = Role;
    public string Content { get; set; } = Content;
}