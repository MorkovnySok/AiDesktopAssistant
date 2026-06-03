using System;
using AiDesktopAssistant.Core.Domain;

namespace AiDesktopAssistant.App.ViewModels;

public class ChatMessageViewModel : ViewModelBase
{
    private string _content = string.Empty;
    private string? _reasoningContent;

    public MessageRole Role { get; }
    public DateTime CreatedAt { get; }

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

    public ChatMessageViewModel(MessageRole role, string content, string? reasoningContent = null, DateTime? createdAt = null)
    {
        Role = role;
        Content = content;
        ReasoningContent = reasoningContent;
        CreatedAt = createdAt ?? DateTime.UtcNow;
    }
}
