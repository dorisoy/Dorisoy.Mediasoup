

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.DateAndTime;

namespace Wpf.Ui.Gallery.Views.Pages.DateAndTime;

[GalleryPage("allows a user to pick a time value.", SymbolRegular.Clock24)]
public partial class TimePickerPage : INavigableView<TimePickerViewModel>
{
    public TimePickerViewModel ViewModel { get; init; }

    public TimePickerPage(TimePickerViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
