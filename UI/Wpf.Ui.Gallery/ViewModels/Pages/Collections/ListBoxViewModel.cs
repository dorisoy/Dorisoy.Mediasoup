

namespace Wpf.Ui.Gallery.ViewModels.Pages.Collections;

public partial class ListBoxViewModel : ViewModel
{
    [ObservableProperty]
    private ObservableCollection<string> _listBoxItems =
    [
        "Arial",
        "Comic Sans MS",
        "Courier New",
        "Segoe UI",
        "Times New Roman",
    ];
}
