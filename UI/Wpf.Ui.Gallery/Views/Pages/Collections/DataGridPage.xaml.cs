

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.Collections;

namespace Wpf.Ui.Gallery.Views.Pages.Collections;

[GalleryPage("Complex data presenter.", SymbolRegular.GridKanban20)]
public partial class DataGridPage : INavigableView<DataGridViewModel>
{
    public DataGridViewModel ViewModel { get; }

    public DataGridPage(DataGridViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
