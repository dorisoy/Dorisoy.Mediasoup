

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.BasicInput;

namespace Wpf.Ui.Gallery.Views.Pages.BasicInput;

[GalleryPage("Button with drop down.", SymbolRegular.Filter16)]
public partial class DropDownButtonPage : INavigableView<DropDownButtonViewModel>
{
    public DropDownButtonViewModel ViewModel { get; }

    public DropDownButtonPage(DropDownButtonViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
