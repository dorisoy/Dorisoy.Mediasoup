

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.DateAndTime;

namespace Wpf.Ui.Gallery.Views.Pages.DateAndTime;

[GalleryPage("Control that lets pick a date.", SymbolRegular.CalendarSearch20)]
public partial class DatePickerPage : INavigableView<DatePickerViewModel>
{
    public DatePickerViewModel ViewModel { get; }

    public DatePickerPage(DatePickerViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
