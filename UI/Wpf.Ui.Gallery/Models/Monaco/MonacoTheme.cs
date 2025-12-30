

namespace Wpf.Ui.Gallery.Models.Monaco;

[Serializable]
public record MonacoTheme
{
    public string? Base { get; init; }

    public bool Inherit { get; init; }

    public IDictionary<string, string>? Rules { get; init; }

    public IDictionary<string, string>? Colors { get; init; }
}
