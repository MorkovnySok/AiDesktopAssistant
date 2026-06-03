using System;
using AiDesktopAssistant.Core.Domain;

namespace AiDesktopAssistant.App.ViewModels;

public class ConversationViewModel : ViewModelBase
{
    private string _title = string.Empty;

    public Guid Id { get; }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public ConversationViewModel(Conversation conversation)
    {
        Id = conversation.Id;
        Title = conversation.Title;
    }
}
