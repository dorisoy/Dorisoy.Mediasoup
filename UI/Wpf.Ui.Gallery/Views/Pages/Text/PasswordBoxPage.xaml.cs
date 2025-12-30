

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.Text;

namespace Wpf.Ui.Gallery.Views.Pages.Text;

[GalleryPage("A control for entering passwords.", SymbolRegular.Password24)]
public partial class PasswordBoxPage : INavigableView<PasswordBoxViewModel>
{
    public PasswordBoxViewModel ViewModel { get; }

    public PasswordBoxPage(PasswordBoxViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
