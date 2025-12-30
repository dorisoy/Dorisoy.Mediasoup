

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.StatusAndInfo;

namespace Wpf.Ui.Gallery.Views.Pages.StatusAndInfo;

[GalleryPage("Shows the app progress on a task.", SymbolRegular.ArrowClockwise24)]
public partial class ProgressRingPage : INavigableView<ProgressRingViewModel>
{
    public ProgressRingViewModel ViewModel { get; }

    public ProgressRingPage(ProgressRingViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
