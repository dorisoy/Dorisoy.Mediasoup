

namespace Wpf.Ui.Controls;

/// <summary>
/// Control changing its properties or appearance depending on the theme.
/// </summary>
public interface IThemeControl
{
    /// <summary>
    /// Gets the theme that is currently set.
    /// </summary>
    public Appearance.ApplicationTheme ApplicationTheme { get; }
}
