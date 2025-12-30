

using Wpf.Ui.Abstractions;

namespace Wpf.Ui.DependencyInjection;

/// <summary>
/// Service that provides pages for navigation.
/// </summary>
public class DependencyInjectionNavigationViewPageProvider(IServiceProvider serviceProvider)
    : INavigationViewPageProvider
{
    /// <inheritdoc />
    public object? GetPage(Type pageType)
    {
        return serviceProvider.GetService(pageType);
    }
}
