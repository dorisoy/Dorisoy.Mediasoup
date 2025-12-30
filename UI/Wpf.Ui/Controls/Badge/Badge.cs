

// https://docs.microsoft.com/en-us/fluent-ui/web-components/components/badge

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Used to highlight an item, attract attention or flag status.
/// </summary>
/// <example>
/// <code lang="xml">
/// &lt;ui:Badge Appearance="Secondary"&gt;
///     &lt;TextBox Text="Hello" /&gt;
/// &lt;/ui:Badge&gt;
/// </code>
/// </example>
public class Badge : System.Windows.Controls.ContentControl, IAppearanceControl
{
    /// <summary>Identifies the <see cref="Appearance"/> dependency property.</summary>
    public static readonly DependencyProperty AppearanceProperty = DependencyProperty.Register(
        nameof(Appearance),
        typeof(Controls.ControlAppearance),
        typeof(Badge),
        new PropertyMetadata(Controls.ControlAppearance.Primary)
    );

    /// <inheritdoc />
    public Controls.ControlAppearance Appearance
    {
        get => (Controls.ControlAppearance)GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }
}
