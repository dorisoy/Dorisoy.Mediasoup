

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ViewModels.Pages.Windows;

namespace Wpf.Ui.Gallery.Views.Pages.Windows;

public partial class WindowsPage : INavigableView<WindowsViewModel>
{
    public WindowsViewModel ViewModel { get; }

    public WindowsPage(WindowsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
