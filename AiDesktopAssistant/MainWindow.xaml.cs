using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AiDesktopAssistant.Models;

namespace AiDesktopAssistant;

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