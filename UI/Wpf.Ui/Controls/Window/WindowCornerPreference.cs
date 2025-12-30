

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Ways you can round windows.
/// </summary>
public enum WindowCornerPreference
{
    /// <summary>
    /// Determined by system or application preference.
    /// </summary>
    Default,

    /// <summary>
    /// Do not round the corners.
    /// </summary>
    DoNotRound,

    /// <summary>
    /// Round the corners.
    /// </summary>
    Round,

    /// <summary>
    /// Round the corners slightly.
    /// </summary>
    RoundSmall,
}
