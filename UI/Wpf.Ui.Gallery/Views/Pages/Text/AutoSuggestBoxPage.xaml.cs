

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.Text;

namespace Wpf.Ui.Gallery.Views.Pages.Text;

[GalleryPage("Control with suggestions.", SymbolRegular.TextBulletListSquare24)]
public partial class AutoSuggestBoxPage : INavigableView<AutoSuggestBoxViewModel>
{
    public AutoSuggestBoxViewModel ViewModel { get; }

    public AutoSuggestBoxPage(AutoSuggestBoxViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
