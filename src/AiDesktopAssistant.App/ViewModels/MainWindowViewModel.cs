using System.Collections.ObjectModel;
using System.Windows.Input;
using AiDesktopAssistant.App.Commands;
using AiDesktopAssistant.App.Services;
using AiDesktopAssistant.Core.Ai;
using AiDesktopAssistant.Core.Entities;
using AiDesktopAssistant.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AiDesktopAssistant.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IChatService _chatService;
    private readonly IConversationRepository _conversationRepository;
    private readonly IGlobalHotkeyService _globalHotkeyService;
    private readonly ISelectedTextCaptureService _selectedTextCaptureService;
    private readonly IMainWindowActivationService _mainWindowActivationService;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly AsyncRelayCommand _sendCommand;
    private ConversationViewModel? _selectedConversation;
    private string _userInput = string.Empty;
    private bool _isStreaming;
    private bool _isCapturingSelection;
    private CancellationTokenSource? _streamingCancellation;

    public MainWindowViewModel(
        IChatService chatService,
        IConversationRepository conversationRepository,
        IGlobalHotkeyService globalHotkeyService,
        ISelectedTextCaptureService selectedTextCaptureService,
        IMainWindowActivationService mainWindowActivationService,
        ILogger<MainWindowViewModel> logger)
    {
        _chatService = chatService;
        _conversationRepository = conversationRepository;
        _globalHotkeyService = globalHotkeyService;
        _selectedTextCaptureService = selectedTextCaptureService;
        _mainWindowActivationService = mainWindowActivationService;
        _logger = logger;
        _sendCommand = new AsyncRelayCommand(SendAsync, CanSend);
        SendCommand = _sendCommand;
        _globalHotkeyService.Pressed += OnGlobalHotkeyPressed;
    }

    public ObservableCollection<ConversationViewModel> Conversations { get; } = [];
    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

    public ICommand SendCommand { get; }

    public ConversationViewModel? SelectedConversation
    {
        get => _selectedConversation;
        set
        {
            if (SetProperty(ref _selectedConversation, value) && value is not null)
            {
                _ = LoadConversationMessagesAsync(value.Id);
            }
        }
    }

    public string UserInput
    {
        get => _userInput;
        set
        {
            if (SetProperty(ref _userInput, value))
            {
                _sendCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsStreaming
    {
        get => _isStreaming;
        private set
        {
            if (SetProperty(ref _isStreaming, value))
            {
                _sendCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Conversations.Clear();
        var conversations = await _conversationRepository.ListAsync(cancellationToken);

        foreach (var conversation in conversations)
        {
            Conversations.Add(new ConversationViewModel(conversation));
        }

        if (Conversations.Count == 0)
        {
            var conversation = await _chatService.CreateConversationAsync(cancellationToken);
            Conversations.Add(new ConversationViewModel(conversation));
        }

        SelectedConversation = Conversations[0];
    }

    private bool CanSend() =>
        !IsStreaming && SelectedConversation is not null && !string.IsNullOrWhiteSpace(UserInput);

    private async Task SendAsync()
    {
        if (SelectedConversation is null)
        {
            return;
        }

        var prompt = UserInput.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        UserInput = string.Empty;
        IsStreaming = true;
        _streamingCancellation = new CancellationTokenSource();

        var userMessage = new ChatMessageViewModel(MessageRole.User, prompt);
        var assistantMessage = new ChatMessageViewModel(MessageRole.Assistant, string.Empty, string.Empty);
        Messages.Add(userMessage);
        Messages.Add(assistantMessage);

        try
        {
            await foreach (var streamEvent in _chatService
                .SendMessageAsync(SelectedConversation.Id, prompt, _streamingCancellation.Token)
                .WithCancellation(_streamingCancellation.Token))
            {
                switch (streamEvent.Type)
                {
                    case AiStreamEventType.Reasoning:
                        assistantMessage.AppendReasoning(streamEvent.Text);
                        break;
                    case AiStreamEventType.Content:
                        assistantMessage.AppendContent(streamEvent.Text);
                        break;
                    case AiStreamEventType.Completed:
                        break;
                }
            }

            await RefreshSelectedConversationAsync(SelectedConversation.Id);
        }
        catch (OperationCanceledException)
        {
            assistantMessage.AppendContent("\n[Request cancelled.]\n");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat request failed.");
            assistantMessage.AppendContent($"\n[Unable to complete request: {ex.Message}]\n");
        }
        finally
        {
            _streamingCancellation?.Dispose();
            _streamingCancellation = null;
            IsStreaming = false;
        }
    }

    private async Task LoadConversationMessagesAsync(Guid conversationId)
    {
        var conversation = await _conversationRepository.GetByIdAsync(conversationId);
        if (conversation is null)
        {
            return;
        }

        Messages.Clear();
        foreach (var message in conversation.Messages.OrderBy(message => message.CreatedAt))
        {
            Messages.Add(new ChatMessageViewModel(message.Role, message.Content, message.ReasoningContent));
        }
    }

    private async Task RefreshSelectedConversationAsync(Guid conversationId)
    {
        var conversation = await _conversationRepository.GetByIdAsync(conversationId);
        if (conversation is null)
        {
            return;
        }

        SelectedConversation?.Refresh(conversation);
        var existing = Conversations.FirstOrDefault(item => item.Id == conversationId);
        existing?.Refresh(conversation);
    }

    private async void OnGlobalHotkeyPressed(object? sender, EventArgs e)
    {
        _logger.LogDebug("Global hotkey handler started.");

        if (_isCapturingSelection)
        {
            _logger.LogDebug("Global hotkey ignored because selected text capture is already running.");
            return;
        }

        _isCapturingSelection = true;

        try
        {
            var selectedText = await _selectedTextCaptureService.CaptureAsync();
            if (string.IsNullOrWhiteSpace(selectedText))
            {
                _logger.LogInformation("Global hotkey capture completed without selected text.");
                return;
            }

            UserInput = string.IsNullOrWhiteSpace(UserInput)
                ? selectedText.Trim()
                : $"{UserInput.TrimEnd()}{Environment.NewLine}{Environment.NewLine}{selectedText.Trim()}";

            _mainWindowActivationService.Activate();
            _logger.LogInformation("Selected text added to chat input. Length: {Length}", selectedText.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to add selected text to the current chat input.");
        }
        finally
        {
            _isCapturingSelection = false;
        }
    }
}
