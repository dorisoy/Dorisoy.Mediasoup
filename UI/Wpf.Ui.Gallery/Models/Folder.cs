

namespace Wpf.Ui.Gallery.Models;

public record Folder
{
    public string Name { get; init; }

    public Folder(string name)
    {
        Name = name;
    }
}
