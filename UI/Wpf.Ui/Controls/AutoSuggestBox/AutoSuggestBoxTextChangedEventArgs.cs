

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Provides data for the <see cref="AutoSuggestBox.TextChanged"/> event.
/// </summary>
public sealed class AutoSuggestBoxTextChangedEventArgs : RoutedEventArgs
{
    public AutoSuggestBoxTextChangedEventArgs(RoutedEvent eventArgs, object sender)
        : base(eventArgs, sender) { }

    public required string Text { get; init; }

    public required AutoSuggestionBoxTextChangeReason Reason { get; init; }
}
