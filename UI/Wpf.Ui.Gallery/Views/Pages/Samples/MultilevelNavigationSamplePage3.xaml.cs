

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ViewModels.Pages.Navigation;

namespace Wpf.Ui.Gallery.Views.Pages.Samples;

public partial class MultilevelNavigationSamplePage3 : INavigableView<MultilevelNavigationSample>
{
    public MultilevelNavigationSamplePage3(MultilevelNavigationSample viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;

        InitializeComponent();
    }

    public MultilevelNavigationSample ViewModel { get; }
}
