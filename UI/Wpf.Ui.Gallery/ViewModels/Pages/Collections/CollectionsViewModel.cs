

using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.Models;
using Wpf.Ui.Gallery.Views.Pages.Collections;

namespace Wpf.Ui.Gallery.ViewModels.Pages.Collections;

public partial class CollectionsViewModel : ViewModel
{
    [ObservableProperty]
    private ICollection<NavigationCard> _navigationCards = new ObservableCollection<NavigationCard>(
        ControlPages
            .FromNamespace(typeof(CollectionsPage).Namespace!)
            .Select(x => new NavigationCard()
            {
                Name = x.Name,
                Icon = x.Icon,
                Description = x.Description,
                PageType = x.PageType,
            })
    );
}
