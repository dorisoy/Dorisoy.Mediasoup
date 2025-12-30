

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.StatusAndInfo;

namespace Wpf.Ui.Gallery.Views.Pages.StatusAndInfo;

/// <summary>
/// Interaction logic for InfoBadgePage.xaml
/// </summary>
[GalleryPage(
    "An non-intrusive UI to display notifications or bring focus to an area",
    SymbolRegular.NumberCircle124
)]
public partial class InfoBadgePage : INavigableView<InfoBadgeViewModel>
{
    public InfoBadgeViewModel ViewModel { get; }

    public InfoBadgePage(InfoBadgeViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
