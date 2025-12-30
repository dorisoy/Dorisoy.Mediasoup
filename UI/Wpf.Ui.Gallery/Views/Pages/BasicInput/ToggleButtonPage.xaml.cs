

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.BasicInput;

namespace Wpf.Ui.Gallery.Views.Pages.BasicInput;

[GalleryPage("Toggleable button.", SymbolRegular.ToggleRight24)]
public partial class ToggleButtonPage : INavigableView<ToggleButtonViewModel>
{
    public ToggleButtonViewModel ViewModel { get; }

    public ToggleButtonPage(ToggleButtonViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
