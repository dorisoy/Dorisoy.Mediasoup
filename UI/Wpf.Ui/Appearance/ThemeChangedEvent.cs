

namespace Wpf.Ui.Appearance;

/// <summary>
/// Event triggered when application theme is updated.
/// </summary>
/// <param name="currentApplicationTheme">Current application <see cref="ApplicationTheme"/>.</param>
/// <param name="systemAccent">Current base system accent <see cref="Color"/>.</param>
public delegate void ThemeChangedEvent(ApplicationTheme currentApplicationTheme, Color systemAccent);
