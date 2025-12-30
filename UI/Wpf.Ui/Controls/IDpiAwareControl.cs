

namespace Wpf.Ui.Controls;

/// <summary>
/// The control that should react to changes in the screen DPI.
/// </summary>
public interface IDpiAwareControl
{
    Hardware.DisplayDpi CurrentWindowDisplayDpi { get; }
}
