

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.Navigation;

namespace Wpf.Ui.Gallery.Views.Pages.Navigation;

[GalleryPage("Shows the trail of navigation taken to the current location.", SymbolRegular.Navigation24)]
public partial class BreadcrumbBarPage : INavigableView<BreadcrumbBarViewModel>
{
    public BreadcrumbBarViewModel ViewModel { get; }

    public BreadcrumbBarPage(BreadcrumbBarViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
