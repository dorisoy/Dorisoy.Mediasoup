

namespace Wpf.Ui.Gallery.ViewModels.Pages.DialogsAndFlyouts;

public partial class FlyoutViewModel : ViewModel
{
    [ObservableProperty]
    private bool _isFlyoutOpen = false;

    [RelayCommand]
    private void OnButtonClick(object sender)
    {
        if (!IsFlyoutOpen)
        {
            IsFlyoutOpen = true;
        }
    }
}
