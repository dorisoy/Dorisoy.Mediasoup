

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.Collections;

namespace Wpf.Ui.Gallery.Views.Pages.Collections;

[GalleryPage("Selectable list.", SymbolRegular.AppsListDetail24)]
public partial class ListBoxPage : INavigableView<ListBoxViewModel>
{
    public ListBoxViewModel ViewModel { get; }

    public ListBoxPage(ListBoxViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
