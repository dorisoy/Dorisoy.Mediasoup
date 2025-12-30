

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.DateAndTime;

namespace Wpf.Ui.Gallery.Views.Pages.DateAndTime;

[GalleryPage("Button opening Calendar.", SymbolRegular.CalendarRtl24)]
public partial class CalendarDatePickerPage : INavigableView<CalendarDatePickerViewModel>
{
    public CalendarDatePickerViewModel ViewModel { get; init; }

    public CalendarDatePickerPage(CalendarDatePickerViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
