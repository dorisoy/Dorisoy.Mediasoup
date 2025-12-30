

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.Collections;

namespace Wpf.Ui.Gallery.Views.Pages.Collections;

[GalleryPage("Selectable list.", SymbolRegular.GroupList24)]
public partial class ListViewPage : INavigableView<ListViewViewModel>
{
    public ListViewViewModel ViewModel { get; }

    public ListViewPage(ListViewViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
