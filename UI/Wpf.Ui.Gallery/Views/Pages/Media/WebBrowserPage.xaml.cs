

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.Media;

namespace Wpf.Ui.Gallery.Views.Pages.Media;

[GalleryPage("(Obsolete) Embedded browser.", SymbolRegular.GlobeProhibited20)]
public partial class WebBrowserPage : INavigableView<WebBrowserViewModel>
{
    public WebBrowserViewModel ViewModel { get; }

    public WebBrowserPage(WebBrowserViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
