

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.StatusAndInfo;

namespace Wpf.Ui.Gallery.Views.Pages.StatusAndInfo;

[GalleryPage("Shows the app progress on a task.", SymbolRegular.ArrowDownload24)]
public partial class ProgressBarPage : INavigableView<ProgressBarViewModel>
{
    public ProgressBarViewModel ViewModel { get; }

    public ProgressBarPage(ProgressBarViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
