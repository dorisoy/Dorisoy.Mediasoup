

namespace Wpf.Ui.Tray;

/// <summary>
/// Singleton containing persistent information about icons in the tray menu for application session.
/// </summary>
internal static class TrayData
{
    /// <summary>
    /// Gets or sets the collection of registered tray icons.
    /// </summary>
    public static List<INotifyIcon> NotifyIcons { get; set; } = new();
}
