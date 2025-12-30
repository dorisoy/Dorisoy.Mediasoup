

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

public class ContentDialogClosingEventArgs : RoutedEventArgs
{
    public ContentDialogClosingEventArgs(RoutedEvent routedEvent, object source)
        : base(routedEvent, source) { }

    public required ContentDialogResult Result { get; init; }

    public bool Cancel { get; set; }
}
