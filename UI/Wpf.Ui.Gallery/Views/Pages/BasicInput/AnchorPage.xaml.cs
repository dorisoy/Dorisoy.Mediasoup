

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.BasicInput;

namespace Wpf.Ui.Gallery.Views.Pages.BasicInput;

[GalleryPage("Button which opens a link.", SymbolRegular.CubeLink20)]
public partial class AnchorPage : INavigableView<AnchorViewModel>
{
    public AnchorViewModel ViewModel { get; init; }

    public AnchorPage(AnchorViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
