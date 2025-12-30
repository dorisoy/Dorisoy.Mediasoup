

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

public class ContentDialogButtonClickEventArgs : RoutedEventArgs
{
    public ContentDialogButtonClickEventArgs(RoutedEvent routedEvent, object source)
        : base(routedEvent, source) { }

    public required ContentDialogButton Button { get; init; }
}
