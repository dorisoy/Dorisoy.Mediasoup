

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.Text;

namespace Wpf.Ui.Gallery.Views.Pages.Text;

[GalleryPage("Control for displaying text.", SymbolRegular.TextCaseLowercase24)]
public partial class TextBlockPage : INavigableView<TextBlockViewModel>
{
    public TextBlockViewModel ViewModel { get; }

    public TextBlockPage(TextBlockViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
