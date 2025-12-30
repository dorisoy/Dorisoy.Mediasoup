

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ViewModels.Pages.DesignGuidance;

namespace Wpf.Ui.Gallery.Views.Pages.DesignGuidance;

/// <summary>
/// Interaction logic for ColorsPage.xaml
/// </summary>
public partial class ColorsPage : INavigableView<ColorsViewModel>
{
    public ColorsViewModel ViewModel { get; }

    public ColorsPage(ColorsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
