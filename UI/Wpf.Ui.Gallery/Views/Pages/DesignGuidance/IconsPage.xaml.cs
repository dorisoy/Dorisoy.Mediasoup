

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ViewModels.Pages.DesignGuidance;

namespace Wpf.Ui.Gallery.Views.Pages.DesignGuidance;

/// <summary>
/// Interaction logic for IconsPage.xaml
/// </summary>
public partial class IconsPage : INavigableView<IconsViewModel>
{
    public IconsViewModel ViewModel { get; }

    public IconsPage(IconsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
