

using System.Windows.Markup;

namespace Wpf.Ui.Markup;

/// <summary>
/// Class for Xaml markup extension for static resource references.
/// </summary>
/// <example>
/// <code lang="xml">
/// &lt;ui:Button
///     Appearance="Primary"
///     Content="WPF UI button with font icon"
///     Foreground={ui:ThemeResource SystemAccentColorPrimaryBrush} /&gt;
/// </code>
/// <code lang="xml">
/// &lt;ui:TextBox Foreground={ui:ThemeResource TextFillColorSecondaryBrush} /&gt;
/// </code>
/// </example>
[TypeConverter(typeof(DynamicResourceExtensionConverter))]
[ContentProperty(nameof(ResourceKey))]
[MarkupExtensionReturnType(typeof(object))]
public class ThemeResourceExtension : DynamicResourceExtension
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeResourceExtension"/> class.
    /// </summary>
    public ThemeResourceExtension()
    {
        ResourceKey = ThemeResource.ApplicationBackgroundBrush.ToString();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeResourceExtension"/> class.
    /// Takes the resource key that this is a static reference to.
    /// </summary>
    public ThemeResourceExtension(ThemeResource resourceKey)
    {
        if (resourceKey == ThemeResource.Unknown)
        {
            throw new ArgumentNullException(nameof(resourceKey));
        }

        ResourceKey = resourceKey.ToString();
    }
}
