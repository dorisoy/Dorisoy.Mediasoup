

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.DialogsAndFlyouts;

namespace Wpf.Ui.Gallery.Views.Pages.DialogsAndFlyouts;

[GalleryPage("Card covering the app content.", SymbolRegular.CalendarMultiple24)]
public partial class ContentDialogPage : INavigableView<ContentDialogViewModel>
{
    public ContentDialogPage(ContentDialogViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;

        InitializeComponent();
    }

    public ContentDialogViewModel ViewModel { get; }
}
