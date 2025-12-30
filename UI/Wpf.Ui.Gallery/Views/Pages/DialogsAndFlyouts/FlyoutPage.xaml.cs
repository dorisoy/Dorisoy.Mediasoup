

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.DialogsAndFlyouts;

namespace Wpf.Ui.Gallery.Views.Pages.DialogsAndFlyouts;

[GalleryPage("Contextual popup.", SymbolRegular.AppTitle24)]
public partial class FlyoutPage : INavigableView<FlyoutViewModel>
{
    public FlyoutViewModel ViewModel { get; }

    public FlyoutPage(FlyoutViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
