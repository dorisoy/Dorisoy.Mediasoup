

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.BasicInput;

namespace Wpf.Ui.Gallery.Views.Pages.BasicInput;

[GalleryPage("Like or dislike.", SymbolRegular.ThumbLike24)]
public partial class ThumbRatePage : INavigableView<ThumbRateViewModel>
{
    public ThumbRateViewModel ViewModel { get; }

    public ThumbRatePage(ThumbRateViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
