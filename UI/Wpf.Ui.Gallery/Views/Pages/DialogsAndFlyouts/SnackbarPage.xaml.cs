

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ViewModels.Pages.DialogsAndFlyouts;

namespace Wpf.Ui.Gallery.Views.Pages.DialogsAndFlyouts;

public partial class SnackbarPage : INavigableView<SnackbarViewModel>
{
    public SnackbarViewModel ViewModel { get; }

    public SnackbarPage(SnackbarViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
