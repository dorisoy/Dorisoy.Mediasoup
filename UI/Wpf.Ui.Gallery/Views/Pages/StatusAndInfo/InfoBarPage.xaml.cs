

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.StatusAndInfo;

namespace Wpf.Ui.Gallery.Views.Pages.StatusAndInfo;

[GalleryPage("Inline message card.", SymbolRegular.ErrorCircle24)]
public partial class InfoBarPage : INavigableView<InfoBarViewModel>
{
    public InfoBarViewModel ViewModel { get; }

    public InfoBarPage(InfoBarViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
