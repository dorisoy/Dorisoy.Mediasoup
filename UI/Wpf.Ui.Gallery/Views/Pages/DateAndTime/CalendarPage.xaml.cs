

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.ViewModels.Pages.DateAndTime;

namespace Wpf.Ui.Gallery.Views.Pages.DateAndTime;

[GalleryPage("Presents a calendar to the user.", SymbolRegular.CalendarLtr24)]
public partial class CalendarPage : INavigableView<CalendarViewModel>
{
    public CalendarViewModel ViewModel { get; }

    public CalendarPage(CalendarViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
