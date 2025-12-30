

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ViewModels.Pages;

namespace Wpf.Ui.Gallery.Views.Pages;

public partial class DashboardPage : INavigableView<DashboardViewModel>
{
    public DashboardViewModel ViewModel { get; }

    public DashboardPage(DashboardViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
