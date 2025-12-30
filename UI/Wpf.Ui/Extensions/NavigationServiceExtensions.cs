

using Wpf.Ui.Controls;

namespace Wpf.Ui.Extensions;

/// <summary>
/// Extensions for the <see cref="INavigationService"/>.
/// </summary>
public static class NavigationServiceExtensions
{
    /// <summary>
    /// Sets the pane display mode of the navigation service.
    /// </summary>
    /// <param name="navigationService">The navigation service.</param>
    /// <param name="paneDisplayMode">The pane display mode.</param>
    /// <returns>Same <see cref="INavigationService"/> so multiple calls can be chained.</returns>
    public static INavigationService SetPaneDisplayMode(
        this INavigationService navigationService,
        NavigationViewPaneDisplayMode paneDisplayMode
    )
    {
        INavigationView? navigationControl = navigationService.GetNavigationControl();

        if (navigationControl is not null)
        {
            navigationControl.PaneDisplayMode = paneDisplayMode;
        }

        return navigationService;
    }
}
