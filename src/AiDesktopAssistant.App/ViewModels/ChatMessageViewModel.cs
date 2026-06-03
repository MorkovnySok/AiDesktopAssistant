using AiDesktopAssistant.Core.Entities;

namespace AiDesktopAssistant.App.ViewModels;

public sealed class ChatMessageViewModel : ViewModelBase
{
    private string _content = string.Empty;
    private string? _reasoningContent;

    public ChatMessageViewModel(MessageRole role, string content, string? reasoningContent = null)
    {
        Role = role;
        _content = content;
        _reasoningContent = reasoningContent;
    }

    public MessageRole Role { get; }

    public string RoleLabel => Role.ToString();

    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    public string? ReasoningContent
    {
        get => _reasoningContent;
        set
        {
            if (SetProperty(ref _reasoningContent, value))
            {
                OnPropertyChanged(nameof(HasReasoning));
            }
        }
    }

    public bool HasReasoning => !string.IsNullOrWhiteSpace(ReasoningContent);

    public void AppendContent(string? text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            Content += text;
        }
    }

    public void AppendReasoning(string? text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            ReasoningContent = (ReasoningContent ?? string.Empty) + text;
        }
    }
}
