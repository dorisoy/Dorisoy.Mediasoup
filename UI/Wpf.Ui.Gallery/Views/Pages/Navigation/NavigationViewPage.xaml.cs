

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.Navigation;

namespace Wpf.Ui.Gallery.Views.Pages.Navigation;

[GalleryPage("Main navigation for the app.", SymbolRegular.PanelLeft24)]
public partial class NavigationViewPage : INavigableView<NavigationViewViewModel>
{
    public NavigationViewViewModel ViewModel { get; }

    public NavigationViewPage(NavigationViewViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
