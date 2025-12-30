

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.StatusAndInfo;

namespace Wpf.Ui.Gallery.Views.Pages.StatusAndInfo;

[GalleryPage("Information in popup window.", SymbolRegular.Comment24)]
public partial class ToolTipPage : INavigableView<ToolTipViewModel>
{
    public ToolTipViewModel ViewModel { get; }

    public ToolTipPage(ToolTipViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
