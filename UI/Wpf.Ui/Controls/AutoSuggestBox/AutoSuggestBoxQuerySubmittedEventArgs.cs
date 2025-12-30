

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Provides event data for the <see cref="AutoSuggestBox.QuerySubmitted"/> event.
/// </summary>
public sealed class AutoSuggestBoxQuerySubmittedEventArgs : RoutedEventArgs
{
    public AutoSuggestBoxQuerySubmittedEventArgs(RoutedEvent eventArgs, object sender)
        : base(eventArgs, sender) { }

    public required string QueryText { get; init; }
}
