

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.BasicInput;

namespace Wpf.Ui.Gallery.Views.Pages.BasicInput;

[GalleryPage("Opens a link.", SymbolRegular.Link24)]
public partial class HyperlinkButtonPage : INavigableView<HyperlinkButtonViewModel>
{
    public HyperlinkButtonViewModel ViewModel { get; }

    public HyperlinkButtonPage(HyperlinkButtonViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
