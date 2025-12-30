

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.Text;

namespace Wpf.Ui.Gallery.Views.Pages.Text;

[GalleryPage("Caption of an item.", SymbolRegular.TextBaseline20)]
public partial class LabelPage : INavigableView<LabelViewModel>
{
    public LabelViewModel ViewModel { get; }

    public LabelPage(LabelViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
