using System.Windows;
using AiDesktopAssistant.App.Services;
using AiDesktopAssistant.App.ViewModels;
using AiDesktopAssistant.Core.Interfaces;
using AiDesktopAssistant.Core.Services;
using AiDesktopAssistant.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiDesktopAssistant.App;

public partial class App : Application
{
    private IHost? _host;
    private AsyncServiceScope? _appScope;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = CreateHostBuilder(e.Args).Build();
        await _host.StartAsync();

        await _host.Services.InitializeDatabaseAsync();

        _appScope = _host.Services.CreateAsyncScope();
        var mainWindow = _appScope.Value.ServiceProvider.GetRequiredService<MainWindow>();
        var viewModel = _appScope.Value.ServiceProvider.GetRequiredService<MainWindowViewModel>();
        var activationService = _appScope.Value.ServiceProvider.GetRequiredService<IAssistantActivationService>();
        mainWindow.DataContext = viewModel;
        await viewModel.InitializeAsync();
        activationService.Attach(mainWindow, viewModel.ActivateFromHotkeyAsync);
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_appScope is not null)
        {
            await _appScope.Value.DisposeAsync();
        }

        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(configuration =>
            {
                configuration.SetBasePath(AppContext.BaseDirectory);
                configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddInfrastructure(context.Configuration);
                services.AddScoped<IChatService, ChatService>();
                services.AddSingleton<ISelectedTextProvider, WindowsClipboardSelectedTextProvider>();
                services.AddSingleton<IAssistantActivationService, WindowsGlobalHotkeyActivationService>();
                services.AddTransient<MainWindow>();
                services.AddTransient<MainWindowViewModel>();
            });
}
