

using Wpf.Ui.Controls;

namespace Wpf.Ui.Gallery.Models;

public struct DisplayableIcon
{
    public int Id { get; set; }

    public string Name { get; set; }

    public string Code { get; set; }

    public string Symbol { get; set; }

    public SymbolRegular Icon { get; set; }
}
