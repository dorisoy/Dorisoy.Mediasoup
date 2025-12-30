

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.Navigation;

namespace Wpf.Ui.Gallery.Views.Pages.Navigation;

[GalleryPage("Contains a collection of MenuItem elements.", SymbolRegular.RowTriple24)]
public partial class MenuPage : INavigableView<MenuViewModel>
{
    public MenuViewModel ViewModel { get; }

    public MenuPage(MenuViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
