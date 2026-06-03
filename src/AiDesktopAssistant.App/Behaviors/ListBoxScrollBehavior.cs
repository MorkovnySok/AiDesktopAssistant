using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ListBox = System.Windows.Controls.ListBox;

namespace AiDesktopAssistant.App.Behaviors;

public static class ListBoxScrollBehavior
{
    public static readonly DependencyProperty AutoScrollToEndProperty = DependencyProperty.RegisterAttached(
        "AutoScrollToEnd",
        typeof(bool),
        typeof(ListBoxScrollBehavior),
        new PropertyMetadata(false, OnAutoScrollToEndChanged));

    private static readonly DependencyProperty CollectionChangedHandlerProperty = DependencyProperty.RegisterAttached(
        "CollectionChangedHandler",
        typeof(NotifyCollectionChangedEventHandler),
        typeof(ListBoxScrollBehavior));

    private static readonly DependencyProperty ItemPropertyChangedHandlerProperty = DependencyProperty.RegisterAttached(
        "ItemPropertyChangedHandler",
        typeof(PropertyChangedEventHandler),
        typeof(ListBoxScrollBehavior));

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
            listBox.Unloaded += OnUnloaded;
            Attach(listBox);
        }
        else
        {
            listBox.Loaded -= OnLoaded;
            listBox.Unloaded -= OnUnloaded;
            Detach(listBox);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ListBox listBox)
        {
            Attach(listBox);
            ScrollToEnd(listBox);
        }
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ListBox listBox)
        {
            Detach(listBox);
        }
    }

    private static void Attach(ListBox listBox)
    {
        Detach(listBox);

        PropertyChangedEventHandler itemPropertyChangedHandler = (_, e) =>
        {
            if (string.IsNullOrEmpty(e.PropertyName)
                || e.PropertyName is "Content" or "ReasoningContent" or "HasReasoning")
            {
                listBox.Dispatcher.BeginInvoke((Action)(() => ScrollToEnd(listBox)));
            }
        };

        listBox.SetValue(ItemPropertyChangedHandlerProperty, itemPropertyChangedHandler);

        foreach (var item in listBox.Items)
        {
            SubscribeToItem(item, itemPropertyChangedHandler);
        }

        if (listBox.ItemsSource is INotifyCollectionChanged notifyCollectionChanged)
        {
            NotifyCollectionChangedEventHandler collectionChangedHandler = (_, e) =>
            {
                if (e.OldItems is not null)
                {
                    foreach (var item in e.OldItems)
                    {
                        UnsubscribeFromItem(item, itemPropertyChangedHandler);
                    }
                }

                if (e.NewItems is not null)
                {
                    foreach (var item in e.NewItems)
                    {
                        SubscribeToItem(item, itemPropertyChangedHandler);
                    }
                }

                listBox.Dispatcher.BeginInvoke((Action)(() => ScrollToEnd(listBox)));
            };

            notifyCollectionChanged.CollectionChanged += collectionChangedHandler;
            listBox.SetValue(CollectionChangedHandlerProperty, collectionChangedHandler);
        }
    }

    private static void Detach(ListBox listBox)
    {
        var itemPropertyChangedHandler =
            (PropertyChangedEventHandler?)listBox.GetValue(ItemPropertyChangedHandlerProperty);

        if (itemPropertyChangedHandler is not null)
        {
            foreach (var item in listBox.Items)
            {
                UnsubscribeFromItem(item, itemPropertyChangedHandler);
            }
        }

        if (listBox.ItemsSource is INotifyCollectionChanged notifyCollectionChanged)
        {
            var collectionChangedHandler =
                (NotifyCollectionChangedEventHandler?)listBox.GetValue(CollectionChangedHandlerProperty);

            if (collectionChangedHandler is not null)
            {
                notifyCollectionChanged.CollectionChanged -= collectionChangedHandler;
            }
        }

        listBox.ClearValue(CollectionChangedHandlerProperty);
        listBox.ClearValue(ItemPropertyChangedHandlerProperty);
    }

    private static void SubscribeToItem(object? item, PropertyChangedEventHandler handler)
    {
        if (item is INotifyPropertyChanged notifyPropertyChanged)
        {
            notifyPropertyChanged.PropertyChanged += handler;
        }
    }

    private static void UnsubscribeFromItem(object? item, PropertyChangedEventHandler handler)
    {
        if (item is INotifyPropertyChanged notifyPropertyChanged)
        {
            notifyPropertyChanged.PropertyChanged -= handler;
        }
    }

    private static void ScrollToEnd(ListBox listBox)
    {
        if (listBox.Items.Count > 0)
        {
            listBox.ScrollIntoView(listBox.Items[listBox.Items.Count - 1]);
        }
    }
}
