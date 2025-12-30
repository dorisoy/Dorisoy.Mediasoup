

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.Media;

namespace Wpf.Ui.Gallery.Views.Pages.Media;

[GalleryPage("Canvas presenter.", SymbolRegular.InkStroke24)]
public partial class CanvasPage : INavigableView<CanvasViewModel>
{
    public CanvasViewModel ViewModel { get; }

    public CanvasPage(CanvasViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
