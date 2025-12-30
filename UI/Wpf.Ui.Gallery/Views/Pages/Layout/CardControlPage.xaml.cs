

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.Layout;

namespace Wpf.Ui.Gallery.Views.Pages.Layout;

/// <summary>
/// Interaction logic for CardControlPage.xaml
/// </summary>
[GalleryPage("Card control.", SymbolRegular.CheckboxIndeterminate24)]
public partial class CardControlPage : INavigableView<CardControlViewModel>
{
    public CardControlPage(CardControlViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
    }

    public CardControlViewModel ViewModel { get; }
}
