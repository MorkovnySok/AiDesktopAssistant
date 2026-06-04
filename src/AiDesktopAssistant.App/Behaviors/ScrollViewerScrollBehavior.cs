using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ItemsControl = System.Windows.Controls.ItemsControl;
using ScrollViewer = System.Windows.Controls.ScrollViewer;

namespace AiDesktopAssistant.App.Behaviors;

public static class ScrollViewerScrollBehavior
{
    public static readonly DependencyProperty AutoScrollToEndProperty = DependencyProperty.RegisterAttached(
        "AutoScrollToEnd",
        typeof(bool),
        typeof(ScrollViewerScrollBehavior),
        new PropertyMetadata(false, OnAutoScrollToEndChanged));

    private static readonly DependencyProperty CollectionChangedHandlerProperty = DependencyProperty.RegisterAttached(
        "CollectionChangedHandler",
        typeof(NotifyCollectionChangedEventHandler),
        typeof(ScrollViewerScrollBehavior));

    private static readonly DependencyProperty ItemPropertyChangedHandlerProperty = DependencyProperty.RegisterAttached(
        "ItemPropertyChangedHandler",
        typeof(PropertyChangedEventHandler),
        typeof(ScrollViewerScrollBehavior));

    private static readonly DependencyProperty ItemsControlProperty = DependencyProperty.RegisterAttached(
        "ItemsControl",
        typeof(ItemsControl),
        typeof(ScrollViewerScrollBehavior));

    public static bool GetAutoScrollToEnd(DependencyObject dependencyObject) =>
        (bool)dependencyObject.GetValue(AutoScrollToEndProperty);

    public static void SetAutoScrollToEnd(DependencyObject dependencyObject, bool value) =>
        dependencyObject.SetValue(AutoScrollToEndProperty, value);

    private static void OnAutoScrollToEndChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ScrollViewer scrollViewer)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            scrollViewer.Loaded += OnLoaded;
            scrollViewer.Unloaded += OnUnloaded;
            scrollViewer.AddHandler(UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(OnPreviewMouseWheel), true);
            Attach(scrollViewer);
        }
        else
        {
            scrollViewer.Loaded -= OnLoaded;
            scrollViewer.Unloaded -= OnUnloaded;
            scrollViewer.RemoveHandler(UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(OnPreviewMouseWheel));
            Detach(scrollViewer);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            Attach(scrollViewer);
            ScrollToEnd(scrollViewer);
        }
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            Detach(scrollViewer);
        }
    }

    private static void Attach(ScrollViewer scrollViewer)
    {
        Detach(scrollViewer);

        var itemsControl = scrollViewer.Content as ItemsControl
            ?? FindDescendant<ItemsControl>(scrollViewer);
        if (itemsControl is null)
        {
            if (scrollViewer.IsLoaded)
            {
                scrollViewer.Dispatcher.BeginInvoke((Action)(() => Attach(scrollViewer)));
            }

            return;
        }

        scrollViewer.SetValue(ItemsControlProperty, itemsControl);

        PropertyChangedEventHandler itemPropertyChangedHandler = (_, e) =>
        {
            if (string.IsNullOrEmpty(e.PropertyName)
                || e.PropertyName is "Content" or "ReasoningContent" or "HasReasoning")
            {
                scrollViewer.Dispatcher.BeginInvoke((Action)(() => ScrollToEnd(scrollViewer)));
            }
        };

        scrollViewer.SetValue(ItemPropertyChangedHandlerProperty, itemPropertyChangedHandler);

        foreach (var item in itemsControl.Items)
        {
            SubscribeToItem(item, itemPropertyChangedHandler);
        }

        if (GetNotifyCollectionChanged(itemsControl) is { } notifyCollectionChanged)
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

                scrollViewer.Dispatcher.BeginInvoke((Action)(() => ScrollToEnd(scrollViewer)));
            };

            notifyCollectionChanged.CollectionChanged += collectionChangedHandler;
            scrollViewer.SetValue(CollectionChangedHandlerProperty, collectionChangedHandler);
        }
    }

    private static void Detach(ScrollViewer scrollViewer)
    {
        var itemsControl = (ItemsControl?)scrollViewer.GetValue(ItemsControlProperty);
        var itemPropertyChangedHandler =
            (PropertyChangedEventHandler?)scrollViewer.GetValue(ItemPropertyChangedHandlerProperty);

        if (itemsControl is not null && itemPropertyChangedHandler is not null)
        {
            foreach (var item in itemsControl.Items)
            {
                UnsubscribeFromItem(item, itemPropertyChangedHandler);
            }
        }

        if (itemsControl is not null && GetNotifyCollectionChanged(itemsControl) is { } notifyCollectionChanged)
        {
            var collectionChangedHandler =
                (NotifyCollectionChangedEventHandler?)scrollViewer.GetValue(CollectionChangedHandlerProperty);

            if (collectionChangedHandler is not null)
            {
                notifyCollectionChanged.CollectionChanged -= collectionChangedHandler;
            }
        }

        scrollViewer.ClearValue(CollectionChangedHandlerProperty);
        scrollViewer.ClearValue(ItemPropertyChangedHandlerProperty);
        scrollViewer.ClearValue(ItemsControlProperty);
    }

    private static INotifyCollectionChanged? GetNotifyCollectionChanged(ItemsControl itemsControl) =>
        itemsControl.ItemsSource as INotifyCollectionChanged
        ?? itemsControl.Items as INotifyCollectionChanged;

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

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || e.Delta == 0)
        {
            return;
        }

        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private static void ScrollToEnd(ScrollViewer scrollViewer)
    {
        scrollViewer.UpdateLayout();
        scrollViewer.ScrollToEnd();
    }

    private static T? FindDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var descendant = FindDescendant<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }
}
