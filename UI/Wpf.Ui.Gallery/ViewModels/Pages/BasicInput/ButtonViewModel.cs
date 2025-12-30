

using System.Windows.Controls;

namespace Wpf.Ui.Gallery.ViewModels.Pages.BasicInput;

public partial class ButtonViewModel : ViewModel
{
    [ObservableProperty]
    private bool _isSimpleButtonEnabled = true;

    [ObservableProperty]
    private bool _isUiButtonEnabled = true;

    [RelayCommand]
    private void OnSimpleButtonCheckboxChecked(object sender)
    {
        if (sender is not CheckBox checkbox)
        {
            return;
        }

        IsSimpleButtonEnabled = !(checkbox?.IsChecked ?? false);
    }

    [RelayCommand]
    private void OnUiButtonCheckboxChecked(object sender)
    {
        if (sender is not CheckBox checkbox)
        {
            return;
        }

        IsUiButtonEnabled = !(checkbox?.IsChecked ?? false);
    }
}
