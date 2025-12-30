

using System.Windows.Controls;

namespace Wpf.Ui.Gallery.ViewModels.Pages.Text;

public partial class AutoSuggestBoxViewModel : ViewModel
{
    [ObservableProperty]
    private List<string> _autoSuggestBoxSuggestions = new()
    {
        "John",
        "Winston",
        "Adrianna",
        "Spencer",
        "Phoebe",
        "Lucas",
        "Carl",
        "Marissa",
        "Brandon",
        "Antoine",
        "Arielle",
        "Arielle",
        "Jamie",
        "Alexzander",
    };

    [ObservableProperty]
    private bool _showClearButton = true;

    [RelayCommand]
    private void OnShowClearButtonChecked(object sender)
    {
        if (sender is not CheckBox checkbox)
        {
            return;
        }

        ShowClearButton = !(checkbox.IsChecked ?? false);
    }
}
