using System;
using System.IO;
using System.Windows;
using AiDesktopAssistant.Core.Interfaces;
using AiDesktopAssistant.Core.Services;
using AiDesktopAssistant.Infrastructure.AI;
using AiDesktopAssistant.Infrastructure.Configuration;
using AiDesktopAssistant.Infrastructure.Persistence;
using AiDesktopAssistant.App.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiDesktopAssistant.App;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, builder) =>
            {
                builder.SetBasePath(AppContext.BaseDirectory);
                builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                // Настройки параметров конфигурации
                services.Configure<AiOptions>(context.Configuration.GetSection("Ai"));
                services.Configure<DatabaseOptions>(context.Configuration.GetSection("Database"));

                // База данных EF Core SQLite
                var dbOptions = context.Configuration.GetSection("Database").Get<DatabaseOptions>();
                services.DbContext<AssistantDbContext>(options =>
                    options.UseSqlite(dbOptions!.ConnectionString));

                // Клиент HTTP с конфигурацией базового адреса Ollama
                var aiOptions = context.Configuration.GetSection("Ai").Get<AiOptions>();
                services.AddHttpClient<IAiChatClient, OllamaChatClient>(client =>
                {
                    client.BaseAddress = new Uri(aiOptions?.BaseUrl ?? "http://localhost:11434");
                    client.Timeout = TimeSpan.FromMinutes(5);
                });

                // Бизнес-логика и репозитории
                services.AddScoped<IConversationRepository, EfConversationRepository>();
                services.AddScoped<IChatService, ChatService>();

                // MVVM слои
                services.AddSingleton<MainWindowViewModel>();
                services.AddTransient<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        // Автоматическое применение миграций или создание SQLite БД
        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AssistantDbContext>();
            db.Database.EnsureCreated(); // Для продакшена лучше db.Database.Migrate();
        }

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        using (_host)
        {
            await _host.StopAsync();
        }
        base.OnExit(e);
    }
}
