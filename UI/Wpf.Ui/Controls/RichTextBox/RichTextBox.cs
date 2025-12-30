

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Extends the <see cref="System.Windows.Controls.RichTextBox"/> control with additional properties.
/// </summary>
public class RichTextBox : System.Windows.Controls.RichTextBox
{
    /// <summary>Identifies the <see cref="IsTextSelectionEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsTextSelectionEnabledProperty = DependencyProperty.Register(
        nameof(IsTextSelectionEnabled),
        typeof(bool),
        typeof(RichTextBox),
        new PropertyMetadata(false)
    );

    /// <summary>
    /// Gets or sets a value indicating whether the user can select text in the control.
    /// </summary>
    public bool IsTextSelectionEnabled
    {
        get => (bool)GetValue(IsTextSelectionEnabledProperty);
        set => SetValue(IsTextSelectionEnabledProperty, value);
    }
}
