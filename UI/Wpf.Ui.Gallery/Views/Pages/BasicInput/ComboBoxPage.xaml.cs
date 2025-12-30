

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.BasicInput;

namespace Wpf.Ui.Gallery.Views.Pages.BasicInput;

[GalleryPage("Button with binary choice.", SymbolRegular.Filter16)]
public partial class ComboBoxPage : INavigableView<ComboBoxViewModel>
{
    public ComboBoxViewModel ViewModel { get; }

    public ComboBoxPage(ComboBoxViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
