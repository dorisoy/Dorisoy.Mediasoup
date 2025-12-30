

namespace Wpf.Ui.Hardware;

/// <summary>
/// Set of tools for hardware acceleration.
/// </summary>
public static class HardwareAcceleration
{
    /// <summary>
    /// Determines whether the provided rendering tier is supported.
    /// </summary>
    /// <param name="tier">Hardware acceleration rendering tier to check.</param>
    /// <returns><see langword="true"/> if tier is supported.</returns>
    public static bool IsSupported(RenderingTier tier)
    {
        return RenderCapability.Tier >> 16 >= (int)tier;
    }
}
