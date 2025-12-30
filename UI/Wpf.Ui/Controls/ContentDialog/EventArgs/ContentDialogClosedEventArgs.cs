

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

public class ContentDialogClosedEventArgs : RoutedEventArgs
{
    public ContentDialogClosedEventArgs(RoutedEvent routedEvent, object source)
        : base(routedEvent, source) { }

    public required ContentDialogResult Result { get; init; }
}
