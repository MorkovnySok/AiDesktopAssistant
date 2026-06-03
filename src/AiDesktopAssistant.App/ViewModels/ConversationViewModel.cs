using AiDesktopAssistant.Core.Entities;

namespace AiDesktopAssistant.App.ViewModels;

public sealed class ConversationViewModel : ViewModelBase
{
    private string _title;
    private DateTime _updatedAt;

    public ConversationViewModel(Conversation conversation)
    {
        Id = conversation.Id;
        _title = conversation.Title;
        _updatedAt = conversation.UpdatedAt;
    }

    public Guid Id { get; }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public DateTime UpdatedAt
    {
        get => _updatedAt;
        set => SetProperty(ref _updatedAt, value);
    }

    public void Refresh(Conversation conversation)
    {
        Title = conversation.Title;
        UpdatedAt = conversation.UpdatedAt;
    }
}
