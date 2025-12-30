

using System.Windows.Controls;

namespace Wpf.Ui.Gallery.ViewModels.Pages.BasicInput;

public partial class HyperlinkButtonViewModel : ViewModel
{
    [ObservableProperty]
    private bool _isHyperlinkEnabled = true;

    [RelayCommand]
    private void OnHyperlinkCheckboxChecked(object sender)
    {
        if (sender is not CheckBox checkbox)
        {
            return;
        }

        IsHyperlinkEnabled = !(checkbox?.IsChecked ?? false);
    }
}
