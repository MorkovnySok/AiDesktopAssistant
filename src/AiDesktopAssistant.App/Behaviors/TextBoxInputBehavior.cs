using System.Windows;
using System.Windows.Input;
using Key = System.Windows.Input.Key;
using Keyboard = System.Windows.Input.Keyboard;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ModifierKeys = System.Windows.Input.ModifierKeys;
using TextBox = System.Windows.Controls.TextBox;

namespace AiDesktopAssistant.App.Behaviors;

public static class TextBoxInputBehavior
{
    public static readonly DependencyProperty SubmitCommandProperty = DependencyProperty.RegisterAttached(
        "SubmitCommand",
        typeof(ICommand),
        typeof(TextBoxInputBehavior),
        new PropertyMetadata(null, OnSubmitCommandChanged));

    public static readonly DependencyProperty MoveCaretToEndSignalProperty = DependencyProperty.RegisterAttached(
        "MoveCaretToEndSignal",
        typeof(int),
        typeof(TextBoxInputBehavior),
        new PropertyMetadata(0, OnMoveCaretToEndSignalChanged));

    public static ICommand? GetSubmitCommand(DependencyObject dependencyObject) =>
        (ICommand?)dependencyObject.GetValue(SubmitCommandProperty);

    public static void SetSubmitCommand(DependencyObject dependencyObject, ICommand? value) =>
        dependencyObject.SetValue(SubmitCommandProperty, value);

    public static int GetMoveCaretToEndSignal(DependencyObject dependencyObject) =>
        (int)dependencyObject.GetValue(MoveCaretToEndSignalProperty);

    public static void SetMoveCaretToEndSignal(DependencyObject dependencyObject, int value) =>
        dependencyObject.SetValue(MoveCaretToEndSignalProperty, value);

    private static void OnSubmitCommandChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not TextBox textBox)
        {
            return;
        }

        if (e.OldValue is not null)
        {
            textBox.PreviewKeyDown -= OnPreviewKeyDown;
        }

        if (e.NewValue is not null)
        {
            textBox.PreviewKeyDown += OnPreviewKeyDown;
        }
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox
            || e.Key != Key.Enter
            || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            return;
        }

        var command = GetSubmitCommand(textBox);
        if (command?.CanExecute(null) != true)
        {
            return;
        }

        e.Handled = true;
        command.Execute(null);
    }

    private static void OnMoveCaretToEndSignalChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not TextBox textBox)
        {
            return;
        }

        textBox.Dispatcher.BeginInvoke((Action)(() =>
        {
            textBox.Focus();
            textBox.CaretIndex = textBox.Text.Length;
            textBox.ScrollToEnd();
        }));
    }
}
