

namespace Wpf.Ui.Controls;

/// <summary>
/// UI <see cref="System.Windows.Controls.Control"/> with <see cref="ControlAppearance"/> attributes.
/// </summary>
public interface IAppearanceControl
{
    /// <summary>
    /// Gets or sets the <see cref="Appearance"/> of the control, if available.
    /// </summary>
    public ControlAppearance Appearance { get; set; }
}
