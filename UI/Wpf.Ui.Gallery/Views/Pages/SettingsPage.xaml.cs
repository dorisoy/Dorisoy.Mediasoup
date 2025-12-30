

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ViewModels.Pages;

namespace Wpf.Ui.Gallery.Views.Pages;

public partial class SettingsPage : INavigableView<SettingsViewModel>
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
