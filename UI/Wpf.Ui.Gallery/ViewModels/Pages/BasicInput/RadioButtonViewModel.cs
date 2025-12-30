

using System.Windows.Controls;

namespace Wpf.Ui.Gallery.ViewModels.Pages.BasicInput;

public partial class RadioButtonViewModel : ViewModel
{
    [ObservableProperty]
    private bool _isRadioButtonEnabled = true;

    [RelayCommand]
    private void OnRadioButtonCheckboxChecked(object sender)
    {
        if (sender is not CheckBox checkbox)
        {
            return;
        }

        IsRadioButtonEnabled = !(checkbox?.IsChecked ?? false);
    }
}
