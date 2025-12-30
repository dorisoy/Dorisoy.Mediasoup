

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.OpSystem;

namespace Wpf.Ui.Gallery.Views.Pages.OpSystem;

[GalleryPage("System file picker.", SymbolRegular.DocumentAdd24)]
public partial class FilePickerPage : INavigableView<FilePickerViewModel>
{
    public FilePickerViewModel ViewModel { get; }

    public FilePickerPage(FilePickerViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
