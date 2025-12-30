

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.BasicInput;

namespace Wpf.Ui.Gallery.Views.Pages.BasicInput;

[GalleryPage("Rating using stars.", SymbolRegular.Star24)]
public partial class RatingPage : INavigableView<RatingViewModel>
{
    public RatingViewModel ViewModel { get; }

    public RatingPage(RatingViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
