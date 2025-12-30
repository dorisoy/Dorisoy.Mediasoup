

using System.Windows.Controls;

namespace Wpf.Ui.Gallery.ViewModels.Pages.BasicInput;

public partial class AnchorViewModel : ViewModel
{
    [ObservableProperty]
    private bool _isAnchorEnabled = true;

    [RelayCommand]
    private void OnAnchorCheckboxChecked(object sender)
    {
        if (sender is not CheckBox checkbox)
        {
            return;
        }

        IsAnchorEnabled = !(checkbox?.IsChecked ?? false);
    }
}
