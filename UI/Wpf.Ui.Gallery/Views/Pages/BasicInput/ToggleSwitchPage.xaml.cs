

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.BasicInput;

namespace Wpf.Ui.Gallery.Views.Pages.BasicInput;

[GalleryPage("Switchable button with a ball.", SymbolRegular.ToggleLeft24)]
public partial class ToggleSwitchPage : INavigableView<ToggleSwitchViewModel>
{
    public ToggleSwitchViewModel ViewModel { get; }

    public ToggleSwitchPage(ToggleSwitchViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
