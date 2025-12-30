

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.BasicInput;

namespace Wpf.Ui.Gallery.Views.Pages.BasicInput;

[GalleryPage("Button with binary choice.", SymbolRegular.CheckmarkSquare24)]
public partial class CheckBoxPage : INavigableView<CheckBoxViewModel>
{
    public CheckBoxViewModel ViewModel { get; }

    public CheckBoxPage(CheckBoxViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
