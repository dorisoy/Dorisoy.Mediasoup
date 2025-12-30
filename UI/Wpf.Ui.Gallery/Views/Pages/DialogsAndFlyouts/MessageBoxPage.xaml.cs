

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.DialogsAndFlyouts;

namespace Wpf.Ui.Gallery.Views.Pages.DialogsAndFlyouts;

[GalleryPage("Message box.", SymbolRegular.CalendarInfo20)]
public partial class MessageBoxPage : INavigableView<MessageBoxViewModel>
{
    public MessageBoxViewModel ViewModel { get; }

    public MessageBoxPage(MessageBoxViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
