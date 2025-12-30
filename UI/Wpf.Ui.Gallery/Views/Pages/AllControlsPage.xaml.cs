

using Wpf.Ui.Gallery.ViewModels.Pages;

namespace Wpf.Ui.Gallery.Views.Pages;

public partial class AllControlsPage : INavigableView<AllControlsViewModel>
{
    public AllControlsViewModel ViewModel { get; }

    public AllControlsPage(AllControlsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
