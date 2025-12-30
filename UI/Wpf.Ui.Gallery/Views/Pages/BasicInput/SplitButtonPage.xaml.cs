

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.BasicInput;

namespace Wpf.Ui.Gallery.Views.Pages.BasicInput;

[GalleryPage("Button with two parts that can be invoked separately.", SymbolRegular.ControlButton24)]
public partial class SplitButtonPage : INavigableView<SplitButtonViewModel>
{
    public SplitButtonViewModel ViewModel { get; }

    public SplitButtonPage(SplitButtonViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
