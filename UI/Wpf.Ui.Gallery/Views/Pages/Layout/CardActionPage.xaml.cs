

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.Layout;

namespace Wpf.Ui.Gallery.Views.Pages.Layout;

/// <summary>
/// Interaction logic for CardActionPage.xaml
/// </summary>
[GalleryPage("Card action control.", SymbolRegular.Code24)]
public partial class CardActionPage : INavigableView<CardActionViewModel>
{
    public CardActionPage(CardActionViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
    }

    public CardActionViewModel ViewModel { get; }
}
