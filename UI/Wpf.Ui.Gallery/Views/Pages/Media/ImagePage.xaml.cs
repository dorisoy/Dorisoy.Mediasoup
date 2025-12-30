

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.Media;

namespace Wpf.Ui.Gallery.Views.Pages.Media;

[GalleryPage("Image presenter.", SymbolRegular.ImageMultiple24)]
public partial class ImagePage : INavigableView<ImageViewModel>
{
    public ImageViewModel ViewModel { get; }

    public ImagePage(ImageViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
