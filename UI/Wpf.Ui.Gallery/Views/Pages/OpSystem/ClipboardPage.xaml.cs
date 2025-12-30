

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.OpSystem;

namespace Wpf.Ui.Gallery.Views.Pages.OpSystem;

[GalleryPage("System clipboard.", SymbolRegular.Desktop24)]
public partial class ClipboardPage : INavigableView<ClipboardViewModel>
{
    public ClipboardViewModel ViewModel { get; }

    public ClipboardPage(ClipboardViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
