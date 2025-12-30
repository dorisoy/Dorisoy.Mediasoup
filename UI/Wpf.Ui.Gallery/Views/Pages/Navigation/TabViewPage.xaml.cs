

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.Navigation;

namespace Wpf.Ui.Gallery.Views.Pages.Navigation;

[GalleryPage("Display a set of tabs.", SymbolRegular.TabDesktop24)]
public partial class TabViewPage : INavigableView<TabViewViewModel>
{
    public TabViewViewModel ViewModel { get; }

    public TabViewPage(TabViewViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
