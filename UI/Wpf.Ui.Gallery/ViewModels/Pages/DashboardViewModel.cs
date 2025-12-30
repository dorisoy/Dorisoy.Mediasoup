

using Wpf.Ui.Gallery.Helpers;

namespace Wpf.Ui.Gallery.ViewModels.Pages;

public partial class DashboardViewModel(INavigationService navigationService) : ViewModel
{
    [RelayCommand]
    private void OnCardClick(string parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter))
        {
            return;
        }

        Type? pageType = NameToPageTypeConverter.Convert(parameter);

        if (pageType == null)
        {
            return;
        }

        _ = navigationService.Navigate(pageType);
    }
}
