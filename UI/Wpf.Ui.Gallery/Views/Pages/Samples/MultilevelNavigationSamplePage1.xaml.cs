

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ViewModels.Pages.Navigation;

namespace Wpf.Ui.Gallery.Views.Pages.Samples;

public partial class MultilevelNavigationSamplePage1 : INavigableView<MultilevelNavigationSample>
{
    public MultilevelNavigationSamplePage1(MultilevelNavigationSample viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;

        InitializeComponent();
    }

    public MultilevelNavigationSample ViewModel { get; }
}
