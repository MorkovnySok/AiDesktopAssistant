namespace AiDesktopAssistant.Infrastructure.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = "Data Source=ai-desktop-assistant.db";
}
