

using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.Models;
using Wpf.Ui.Gallery.Views.Pages.StatusAndInfo;

namespace Wpf.Ui.Gallery.ViewModels.Pages.StatusAndInfo;

public partial class StatusAndInfoViewModel : ViewModel
{
    [ObservableProperty]
    private ICollection<NavigationCard> _navigationCards = new ObservableCollection<NavigationCard>(
        ControlPages
            .FromNamespace(typeof(StatusAndInfoPage).Namespace!)
            .Select(x => new NavigationCard()
            {
                Name = x.Name,
                Icon = x.Icon,
                Description = x.Description,
                PageType = x.PageType,
            })
    );
}
