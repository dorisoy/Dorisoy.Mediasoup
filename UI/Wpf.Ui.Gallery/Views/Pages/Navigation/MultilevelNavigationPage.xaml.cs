

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.Navigation;

namespace Wpf.Ui.Gallery.Views.Pages.Navigation;

[GalleryPage("Navigation with multi level Breadcrumb.", SymbolRegular.PanelRightContract24)]
public partial class MultilevelNavigationPage : INavigableView<MultilevelNavigationSample>
{
    public MultilevelNavigationPage(MultilevelNavigationSample viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;

        InitializeComponent();
    }

    public MultilevelNavigationSample ViewModel { get; }
}
