using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace AiDesktopAssistant.App.Behaviors;

public static class ListBoxScrollBehavior
{
    public static readonly DependencyProperty AutoScrollToEndProperty = DependencyProperty.RegisterAttached(
        "AutoScrollToEnd",
        typeof(bool),
        typeof(ListBoxScrollBehavior),
        new PropertyMetadata(false, OnAutoScrollToEndChanged));

    public static bool GetAutoScrollToEnd(DependencyObject dependencyObject) =>
        (bool)dependencyObject.GetValue(AutoScrollToEndProperty);

    public static void SetAutoScrollToEnd(DependencyObject dependencyObject, bool value) =>
        dependencyObject.SetValue(AutoScrollToEndProperty, value);

    private static void OnAutoScrollToEndChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ListBox listBox)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            listBox.Loaded += OnLoaded;
            listBox.TargetUpdated += (_, _) => ScrollToEnd(listBox);
        }
        else
        {
            listBox.Loaded -= OnLoaded;
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        if (listBox.ItemsSource is INotifyCollectionChanged notifyCollectionChanged)
        {
            notifyCollectionChanged.CollectionChanged += (_, _) => ScrollToEnd(listBox);
        }

        ScrollToEnd(listBox);
    }

    private static void ScrollToEnd(ListBox listBox)
    {
        if (listBox.Items.Count > 0)
        {
            listBox.ScrollIntoView(listBox.Items[listBox.Items.Count - 1]);
        }
    }
}
