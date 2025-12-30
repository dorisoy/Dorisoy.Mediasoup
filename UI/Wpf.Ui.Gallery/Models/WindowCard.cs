

using Wpf.Ui.Controls;

namespace Wpf.Ui.Gallery.Models;

public record WindowCard
{
    public string Name { get; set; }

    public string Description { get; init; }

    public SymbolRegular Icon { get; init; }

    public string Value { get; set; }

    public WindowCard(string name, string description, SymbolRegular icon, string value)
    {
        Name = name;
        Description = description;
        Icon = icon;
        Value = value;
    }
}
