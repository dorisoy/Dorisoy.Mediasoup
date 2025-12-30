

namespace Wpf.Ui.Designer;

/// <summary>
/// Helper class for Visual Studio designer.
/// </summary>
public static class DesignerHelper
{
    private static bool _isValueAlreadyValidated = default;

    private static bool _isInDesignMode = default;

    /// <summary>
    /// Gets a value indicating whether the project is currently in design mode.
    /// </summary>
    public static bool IsInDesignMode => IsCurrentAppInDebugMode();

    /// <summary>
    /// Gets a value indicating whether the project is currently debugged.
    /// </summary>
    public static bool IsDebugging => System.Diagnostics.Debugger.IsAttached;

    private static bool IsCurrentAppInDebugMode()
    {
        if (_isValueAlreadyValidated)
        {
            return _isInDesignMode;
        }

        _isInDesignMode = (bool)(
            DesignerProperties.IsInDesignModeProperty.GetMetadata(typeof(DependencyObject))?.DefaultValue
            ?? false
        );

        _isValueAlreadyValidated = true;

        return _isInDesignMode;
    }
}
