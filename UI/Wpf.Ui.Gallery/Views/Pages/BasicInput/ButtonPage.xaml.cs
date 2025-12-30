

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.BasicInput;

namespace Wpf.Ui.Gallery.Views.Pages.BasicInput;

[GalleryPage("Simple button.", SymbolRegular.ControlButton24)]
public partial class ButtonPage : INavigableView<ButtonViewModel>
{
    public ButtonViewModel ViewModel { get; }

    public ButtonPage(ButtonViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
