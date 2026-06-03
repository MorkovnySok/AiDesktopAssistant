using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AiDesktopAssistant.Core.Domain;
using AiDesktopAssistant.Core.Interfaces;

namespace AiDesktopAssistant.App.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IChatService _chatService;
    private readonly IConversationRepository _repository;

    private ObservableCollection<ConversationViewModel> _conversations = new();
    private ObservableCollection<ChatMessageViewModel> _messages = new();
    private ConversationViewModel? _selectedConversation;
    private string _userInput = string.Empty;
    private bool _isSending;
    private CancellationTokenSource? _cts;

    public ObservableCollection<ConversationViewModel> Conversations
    {
        get => _conversations;
        set => SetProperty(ref _conversations, value);
    }

    public ObservableCollection<ChatMessageViewModel> Messages
    {
        get => _messages;
        set => SetProperty(ref _messages, value);
    }

    public ConversationViewModel? SelectedConversation
    {
        get => _selectedConversation;
        set
        {
            if (SetProperty(ref _selectedConversation, value))
            {
                _ = LoadMessagesForCurrentConversationAsync();
            }
        }
    }

    public string UserInput
    {
        get => _userInput;
        set => SetProperty(ref _userInput, value);
    }

    public bool IsSending
    {
        get => _isSending;
        set => SetProperty(ref _isSending, value);
    }

    public ICommand SendMessageCommand { get; }
    public ICommand CreateConversationCommand { get; }

    public MainWindowViewModel(IChatService chatService, IConversationRepository repository)
    {
        _chatService = chatService;
        _repository = repository;

        SendMessageCommand = new RelayCommand(async _ => await SendMessageInternalAsync(), _ => !IsSending && !string.IsNullOrWhiteSpace(UserInput));
        CreateConversationCommand = new RelayCommand(async _ => await CreateNewConversationAsync());

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadConversationsListAsync();

        if (Conversations.Count == 0)
        {
            await CreateNewConversationAsync();
        }
        else
        {
            SelectedConversation = Conversations[0];
        }
    }

    private async Task LoadConversationsListAsync()
    {
        var list = await _repository.ListAsync();
        Conversations.Clear();
        foreach (var c in list)
        {
            Conversations.Add(new ConversationViewModel(c));
        }
    }

    private async Task LoadMessagesForCurrentConversationAsync()
    {
        Messages.Clear();
        if (SelectedConversation == null) return;

        var fullConversation = await _repository.GetByIdAsync(SelectedConversation.Id);
        if (fullConversation != null)
        {
            foreach (var msg in fullConversation.Messages)
            {
                Messages.Add(new ChatMessageViewModel(msg.Role, msg.Content, msg.ReasoningContent, msg.CreatedAt));
            }
        }
    }

    private async Task CreateNewConversationAsync()
    {
        var model = await _chatService.CreateConversationAsync();
        var vm = new ConversationViewModel(model);
        Conversations.Insert(0, vm);
        SelectedConversation = vm;
    }

    private async Task SendMessageInternalAsync()
    {
        if (SelectedConversation == null || string.IsNullOrWhiteSpace(UserInput)) return;

        var textToSend = UserInput.Trim();
        UserInput = string.Empty;
        IsSending = true;

        // 1. Сразу же выводим сообщение пользователя в UI
        Messages.Add(new ChatMessageViewModel(MessageRole.User, textToSend));

        // 2. Создаем пустое сообщение ИИ ассистента для стриминга токенов
        var assistantVm = new ChatMessageViewModel(MessageRole.Assistant, string.Empty, string.Empty);
        Messages.Add(assistantVm);

        _cts = new CancellationTokenSource();

        try
        {
            var eventStream = await _chatService.SendMessageAsync(SelectedConversation.Id, textToSend, _cts.Token);

            await foreach (var @event in eventStream.WithCancellation(_cts.Token))
            {
                if (@event.Type == AiStreamEventType.Reasoning)
                {
                    assistantVm.ReasoningContent += @event.Text;
                }
                else if (@event.Type == AiStreamEventType.Content)
                {
                    assistantVm.Content += @event.Text;
                }
            }
        }
        catch (OperationCanceledException)
        {
            assistantVm.Content += "\n[Генерация ответа остановлена пользователем]";
        }
        catch (Exception ex)
        {
            assistantVm.Content += $"\n[Внутренняя ошибка приложения]: {ex.Message}";
        }
        finally
        {
            IsSending = false;
            _cts.Dispose();
            _cts = null;
            // Обновляем список тредов, так как заголовок мог измениться
            await LoadConversationsListAsync();
        }
    }
}
