

namespace Wpf.Ui.Gallery.ViewModels.Pages.Navigation;

public partial class MultilevelNavigationSample(INavigationService navigationService)
{
    [RelayCommand]
    private void NavigateForward(Type type)
    {
        _ = navigationService.NavigateWithHierarchy(type);
    }

    [RelayCommand]
    private void NavigateBack()
    {
        _ = navigationService.GoBack();
    }
}
