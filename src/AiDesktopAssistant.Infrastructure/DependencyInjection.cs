using System.IO;
using AiDesktopAssistant.Core.Ai;
using AiDesktopAssistant.Core.Interfaces;
using AiDesktopAssistant.Infrastructure.Ollama;
using AiDesktopAssistant.Infrastructure.Options;
using AiDesktopAssistant.Infrastructure.Persistence;
using AiDesktopAssistant.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiDesktopAssistant.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));

        services.AddDbContext<AssistantDbContext>((provider, options) =>
        {
            var databaseOptions = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseSqlite(databaseOptions.ConnectionString);
        });

        services.AddHttpClient<OllamaChatClient>((provider, client) =>
        {
            var aiOptions = provider.GetRequiredService<IOptions<AiOptions>>().Value;
            client.BaseAddress = new Uri(aiOptions.BaseUrl);
            client.Timeout = Timeout.InfiniteTimeSpan;
        });

        services.AddScoped<IConversationRepository, EfConversationRepository>();
        services.AddScoped<IAiChatClient>(provider => provider.GetRequiredService<OllamaChatClient>());

        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AssistantDbContext>();
        var connectionString = db.Database.GetConnectionString();
        var databasePath = GetSqliteDatabasePath(connectionString);

        if (!string.IsNullOrWhiteSpace(databasePath))
        {
            var directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        await db.Database.EnsureCreatedAsync();
    }

    private static string? GetSqliteDatabasePath(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        const string key = "Data Source=";
        var start = connectionString.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        start += key.Length;
        var end = connectionString.IndexOf(';', start);
        return end < 0 ? connectionString[start..] : connectionString[start..end];
    }
}
