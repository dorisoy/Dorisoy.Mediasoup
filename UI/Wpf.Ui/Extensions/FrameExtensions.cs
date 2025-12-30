

using System.Windows.Controls;

namespace Wpf.Ui.Extensions;

/// <summary>
/// Set of extensions for <see cref="System.Windows.Controls.Frame"/>.
/// </summary>
public static class FrameExtensions
{
    /// <summary>
    /// Gets <see cref="FrameworkElement.DataContext"/> from <see cref="Frame"/>.
    /// </summary>
    /// <param name="frame">Selected frame.</param>
    /// <returns>DataContext of currently active element, otherwise <see langword="null"/>.</returns>
    public static object? GetDataContext(this Frame frame)
    {
        return frame.Content is not FrameworkElement element ? null : element.DataContext;
    }

    /// <summary>
    /// Cleans <see cref="Frame"/> journal.
    /// </summary>
    /// <param name="frame">Selected frame.</param>
    public static void CleanNavigation(this Frame frame)
    {
        while (frame.CanGoBack)
        {
            _ = frame.RemoveBackEntry();
        }
    }
}
