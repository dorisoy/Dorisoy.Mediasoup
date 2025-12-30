

using Wpf.Ui.Gallery.ControlsLookup;
using Wpf.Ui.Gallery.Models;
using Wpf.Ui.Gallery.Views.Pages.Layout;

namespace Wpf.Ui.Gallery.ViewModels.Pages.Layout;

public partial class LayoutViewModel : ViewModel
{
    [ObservableProperty]
    private ICollection<NavigationCard> _navigationCards = new ObservableCollection<NavigationCard>(
        ControlPages
            .FromNamespace(typeof(LayoutPage).Namespace!)
            .Select(x => new NavigationCard()
            {
                Name = x.Name,
                Icon = x.Icon,
                Description = x.Description,
                PageType = x.PageType,
            })
    );
}
