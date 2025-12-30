

// ReSharper disable once CheckNamespace
using System.Windows.Automation.Peers;

namespace Wpf.Ui.Controls;

/// <summary>
/// Inherited from the <see cref="System.Windows.Controls.Primitives.ButtonBase"/> interactive card styled according to Fluent Design.
/// </summary>
public class CardAction : System.Windows.Controls.Primitives.ButtonBase
{
    /// <summary>Identifies the <see cref="IsChevronVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsChevronVisibleProperty = DependencyProperty.Register(
        nameof(IsChevronVisible),
        typeof(bool),
        typeof(CardAction),
        new PropertyMetadata(true)
    );

    /// <summary>Identifies the <see cref="Icon"/> dependency property.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(IconElement),
        typeof(CardAction),
        new PropertyMetadata(null, null, IconElement.Coerce)
    );

    /// <summary>
    /// Gets or sets a value indicating whether to display the chevron icon on the right side of the card.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public bool IsChevronVisible
    {
        get => (bool)GetValue(IsChevronVisibleProperty);
        set => SetValue(IsChevronVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets displayed <see cref="IconElement"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public IconElement? Icon
    {
        get => (IconElement?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new CardActionAutomationPeer(this);
    }
}
