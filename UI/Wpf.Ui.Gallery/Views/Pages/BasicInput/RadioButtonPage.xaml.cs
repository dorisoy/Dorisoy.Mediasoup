

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.BasicInput;

namespace Wpf.Ui.Gallery.Views.Pages.BasicInput;

[GalleryPage("Set of options as buttons.", SymbolRegular.RadioButton24)]
public partial class RadioButtonPage : INavigableView<RadioButtonViewModel>
{
    public RadioButtonViewModel ViewModel { get; }

    public RadioButtonPage(RadioButtonViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
