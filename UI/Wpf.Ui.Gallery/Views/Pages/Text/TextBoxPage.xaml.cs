

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.Text;

namespace Wpf.Ui.Gallery.Views.Pages.Text;

[GalleryPage("Plain text field.", SymbolRegular.TextColor24)]
public partial class TextBoxPage : INavigableView<TextBoxViewModel>
{
    public TextBoxViewModel ViewModel { get; }

    public TextBoxPage(TextBoxViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
