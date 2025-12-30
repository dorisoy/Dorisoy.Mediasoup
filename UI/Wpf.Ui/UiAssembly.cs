

using System.Reflection;

namespace Wpf.Ui;

/// <summary>
/// Allows to get the WPF UI assembly through <see cref="Assembly"/>.
/// </summary>
public static class UiAssembly
{
    /// <summary>
    /// Gets the WPF UI assembly.
    /// </summary>
    public static Assembly Assembly => Assembly.GetExecutingAssembly();
}
