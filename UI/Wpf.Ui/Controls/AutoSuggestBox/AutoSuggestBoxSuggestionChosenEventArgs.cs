

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Provides data for the <see cref="AutoSuggestBox.SuggestionChosen"/> event.
/// </summary>
public sealed class AutoSuggestBoxSuggestionChosenEventArgs : RoutedEventArgs
{
    public AutoSuggestBoxSuggestionChosenEventArgs(RoutedEvent eventArgs, object sender)
        : base(eventArgs, sender) { }

    public required object SelectedItem { get; init; }
}
