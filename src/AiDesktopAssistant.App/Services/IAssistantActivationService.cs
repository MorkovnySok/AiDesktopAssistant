namespace AiDesktopAssistant.App.Services;

public interface IAssistantActivationService : IDisposable
{
    void Attach(MainWindow window, Func<Task<string?>> activateAsync);
}
