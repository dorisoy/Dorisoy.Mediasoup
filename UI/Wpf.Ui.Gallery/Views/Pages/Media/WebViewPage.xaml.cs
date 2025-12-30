

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.Media;

namespace Wpf.Ui.Gallery.Views.Pages.Media;

[GalleryPage("Embedded browser window.", SymbolRegular.GlobeDesktop24)]
public partial class WebViewPage : INavigableView<WebViewViewModel>
{
    public WebViewViewModel ViewModel { get; }

    public WebViewPage(WebViewViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
