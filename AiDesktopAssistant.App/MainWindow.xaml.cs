using System.Collections.ObjectModel;
using System.Windows;
using AiDesktopAssistant.Core.Domain;

namespace AiDesktopAssistant.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ObservableCollection<ChatMessage> _messages = [];
    
    public MainWindow()
    {
        InitializeComponent();
        ChatMessages.ItemsSource = _messages;
    }

    private void SendButtonClick(object sender, RoutedEventArgs e)
    {
        var question = UserInput.Text.Trim();
        if (string.IsNullOrEmpty(question))
            return;

        _messages.Add(new ChatMessage("User", question));
        _messages.Add(new ChatMessage("Assistant", "TODO"));
        
        UserInput.Clear();
        UserInput.Focus();
    }
}