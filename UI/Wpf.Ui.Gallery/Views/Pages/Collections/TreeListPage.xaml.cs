

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.Collections;

namespace Wpf.Ui.Gallery.Views.Pages.Collections;

[GalleryPage("Collapsable list.", SymbolRegular.TextBulletListTree24)]
public partial class TreeListPage : INavigableView<TreeListViewModel>
{
    public TreeListViewModel ViewModel { get; }

    public TreeListPage(TreeListViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
