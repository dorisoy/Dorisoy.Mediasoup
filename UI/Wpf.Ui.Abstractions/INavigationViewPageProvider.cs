

namespace Wpf.Ui.Abstractions;

/// <summary>
/// Defines a service that provides pages for navigation.
/// </summary>
public interface INavigationViewPageProvider
{
    /// <summary>
    /// Retrieves a page of the specified type.
    /// </summary>
    /// <param name="pageType">The type of the page to retrieve.</param>
    /// <returns>An instance of the specified page type, or null if the page is not found.</returns>
    public object? GetPage(Type pageType);
}
