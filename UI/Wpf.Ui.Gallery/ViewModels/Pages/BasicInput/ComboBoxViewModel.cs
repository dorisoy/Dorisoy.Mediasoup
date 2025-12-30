

namespace Wpf.Ui.Gallery.ViewModels.Pages.BasicInput;

public partial class ComboBoxViewModel : ViewModel
{
    [ObservableProperty]
    private ObservableCollection<string> _comboBoxFontFamilies =
    [
        "Arial",
        "Comic Sans MS",
        "Segoe UI",
        "Times New Roman",
    ];

    [ObservableProperty]
    private ObservableCollection<int> _comboBoxFontSizes =
    [
        8,
        9,
        10,
        11,
        12,
        14,
        16,
        18,
        20,
        24,
        28,
        36,
        48,
        72,
    ];
}
