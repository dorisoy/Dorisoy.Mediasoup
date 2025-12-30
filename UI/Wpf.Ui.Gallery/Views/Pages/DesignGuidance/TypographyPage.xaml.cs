

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ViewModels.Pages.DesignGuidance;

namespace Wpf.Ui.Gallery.Views.Pages.DesignGuidance;

public partial class TypographyPage : INavigableView<TypographyViewModel>
{
    public TypographyViewModel ViewModel { get; }

    public TypographyPage(TypographyViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
