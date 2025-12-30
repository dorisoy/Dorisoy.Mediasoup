

namespace Wpf.Ui.Abstractions.Controls;

/// <summary>
/// A component whose ViewModel is separate from the DataContext and can be navigated by INavigationView.
/// </summary>
/// <typeparam name="T">The type of the ViewModel associated with the view. This type optionally may implement <see cref="INavigationAware"/> to participate in navigation processes.</typeparam>
public interface INavigableView<out T>
{
    /// <summary>
    /// Gets the view model used by the view.
    /// Optionally, it may implement <see cref="INavigationAware"/> and be navigated by INavigationView.
    /// </summary>
    T ViewModel { get; }
}
