

namespace Wpf.Ui.Gallery.ControlsLookup;

internal static class ControlPages
{
    private const string PageSuffix = "Page";

    public static IEnumerable<GalleryPage> All()
    {
        foreach (
            Type? type in GalleryAssembly
                .Asssembly.GetTypes()
                .Where(t => t.IsDefined(typeof(GalleryPageAttribute)))
        )
        {
            GalleryPageAttribute? galleryPageAttribute = type.GetCustomAttributes<GalleryPageAttribute>()
                .FirstOrDefault();

            if (galleryPageAttribute is not null)
            {
                yield return new GalleryPage(
                    type.Name[..type.Name.LastIndexOf(PageSuffix)],
                    galleryPageAttribute.Description,
                    galleryPageAttribute.Icon,
                    type
                );
            }
        }
    }

    public static IEnumerable<GalleryPage> FromNamespace(string namespaceName)
    {
        return All().Where(t => t.PageType?.Namespace?.StartsWith(namespaceName) ?? false);
    }
}
