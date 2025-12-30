

using System.Windows.Controls;

namespace Wpf.Ui.Gallery.ViewModels.Pages.BasicInput;

public partial class ToggleButtonViewModel : ViewModel
{
    [ObservableProperty]
    private bool _isToggleButtonEnabled = true;

    [RelayCommand]
    private void OnToggleButtonCheckboxChecked(object sender)
    {
        if (sender is not CheckBox checkbox)
        {
            return;
        }

        IsToggleButtonEnabled = !(checkbox?.IsChecked ?? false);
    }
}
