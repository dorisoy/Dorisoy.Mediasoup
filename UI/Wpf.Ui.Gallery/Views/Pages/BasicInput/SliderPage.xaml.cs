

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.BasicInput;

namespace Wpf.Ui.Gallery.Views.Pages.BasicInput;

[GalleryPage("Sliding control.", SymbolRegular.HandDraw24)]
public partial class SliderPage : INavigableView<SliderViewModel>
{
    public SliderViewModel ViewModel { get; }

    public SliderPage(SliderViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
