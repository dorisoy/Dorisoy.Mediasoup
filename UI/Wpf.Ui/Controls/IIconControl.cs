

namespace Wpf.Ui.Controls;

/// <summary>
/// Control that allows you to set an icon in it with an <see cref="Icon"/>.
/// </summary>
public interface IIconControl
{
    /// <summary>
    /// Gets or sets displayed <see cref="IconElement"/>.
    /// </summary>
    IconElement? Icon { get; set; }
}
