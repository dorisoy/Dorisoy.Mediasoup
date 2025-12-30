

using System.Windows.Controls;

namespace Wpf.Ui.Gallery.ViewModels.Pages.BasicInput;

public partial class ToggleSwitchViewModel : ViewModel
{
    [ObservableProperty]
    private bool _isToggleSwitchEnabled = true;

    [RelayCommand]
    private void OnToggleSwitchCheckboxChecked(object sender)
    {
        if (sender is not CheckBox checkbox)
        {
            return;
        }

        IsToggleSwitchEnabled = !(checkbox?.IsChecked ?? false);
    }
}
